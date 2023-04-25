using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Threading.Tasks.Task;

namespace ServerProgram;

internal static class Program
{
    private static async Task Main()
    {
        try
        {
            var localAddr = IPAddress.Parse(IPAddress.Loopback.ToString());
            var broadband = IPAddress.Parse("127.0.0.255");
            var listener = new TcpListener(localAddr, 29000)
            {
                ExclusiveAddressUse = true
            };
            listener.Start();

            Console.WriteLine("Server started.");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                Console.WriteLine("FreeClient connected.");

                _ = HandleClientAsync(client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.HelpLink);
        }

        Console.ReadKey();
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        var cts = new CancellationTokenSource();

        try
        {
            int newPort;
            try
            {
                newPort = FindAvailablePort(29001, 30000); // Найти следующий доступный порт в отрезке
            }
            catch (Exception e)
            {
                client.GetStream().Write(Encoding.ASCII.GetBytes(e.Message));
                client.Close();
                return;
            }

            var message = $"Redirected to port {newPort}";

            // Отправляем новый номер порта клиенту
            var data = Encoding.ASCII.GetBytes(message);
            await client.GetStream().WriteAsync(data, cts.Token);

            var newListener = new TcpListener(IPAddress.Parse(IPAddress.Loopback.ToString()), newPort)
            {
                ExclusiveAddressUse = true
            };
            newListener.Start();


            var receive = Run(() => ReceiveMessagesAsync(client, newPort, cts.Token), cts.Token);

            await WhenAll(receive);

            newListener.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            cts.Cancel();
            client.Close();
        }
    }

    private static int FindAvailablePort(int startingPort, int maxPort)
    {
        var port = startingPort;

        while (port <= maxPort)
        {
            var listener = new TcpListener(IPAddress.Parse(IPAddress.Loopback.ToString()), port);

            try
            {
                listener.Start();
                return port;
            }
            catch
            {
                port++;
            }
            finally
            {
                listener.Stop();
            }
        }

        throw new Exception("No available ports found");
    }

    private static async Task ReceiveMessagesAsync(TcpClient client, int localPort, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[1024];
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await client.GetStream().ReadAsync(buffer, cancellationToken);
                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.Write($"client_{localPort}: ");
                Console.WriteLine($"Received message: {message}");

                _ = Run(() => SendAnswer(cancellationToken, message, client), cancellationToken);

                await Delay(10, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Console.Write($"client_{localPort}: ");
            Console.WriteLine("Receive messages canceled.");
        }
        catch (Exception ex)
        {
            Console.Write($"client_{localPort}: ");
            Console.WriteLine(ex.Message);
        }
        finally
        {
            CloseConnection(client);
        }
    }

    private static async Task SendAnswer(this CancellationToken cancellationToken, string message, TcpClient client)
    {
        string? answer = null;
        if (message.EndsWith(".txt"))
        {
            if (File.Exists(message))
            {
                _ = Run(() => GetTxtContent(cancellationToken, message, client), cancellationToken);
                return;
            }
            else if (Directory.Exists(message))
                answer = GetListFiles(message);
        }
        else if (Directory.Exists(message))
            answer = GetListFiles(message);

        if (answer is null)
        {
            Console.WriteLine(
                $"send txtfile or directory {message} does not exist"); //todo: maybe implement not only txt files
            answer = $"! TxtFile or directory {message} does not exist";
        }

        if (answer == "")
            answer = "!This directory is empty.";

        await SendAsync(cancellationToken, client, answer);
        await SendAsync(cancellationToken, client, "EndOfFile");
    }

    private static async Task SendAsync(CancellationToken cancellationToken, TcpClient client, string answer)
    {
        Debug.Assert(answer != null, nameof(answer) + " != null");
        var data = Encoding.UTF8.GetBytes(answer);
        var offset = 0;
        while (offset < data.Length)
        {
            var bytesToSend = Math.Min(1024, data.Length - offset);
            await client.GetStream().WriteAsync(data.AsMemory(offset, bytesToSend), cancellationToken);
            offset += bytesToSend;
        }
    }

    private static string GetListFiles(string message)
    {
        var entries = GetAllDirectoryEntries(message);
        var answerBuilder = new StringBuilder();
        foreach (var entry in entries)
        {
            answerBuilder.AppendLine(entry);
        }

        var answer = "!" + answerBuilder;
        return answer;
    }

    private static async Task GetTxtContent(CancellationToken cancellationToken, string message, TcpClient client)
    {
        await SendAsync(cancellationToken, client, $"!TxtContent of {message}: ");
        await using (var fileStream = new FileStream(message, FileMode.Open, FileAccess.Read))
        {
            var buffer = new byte[1024]; // read 1KB at a time
            var bytesRead = 0;
            while ((bytesRead = await fileStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Sending chunk of {message}: {chunk}");
                await SendAsync(cancellationToken, client, chunk);
            }
        }

        // after sending whole file send EndOfFile
        await SendAsync(cancellationToken, client, "EndOfFile");
    }

    private static List<string> GetAllDirectoryEntries(string directoryPath)
    {
        var entries = new List<string>();

        try
        {
            entries.AddRange(Directory.GetDirectories(directoryPath));
            entries.AddRange(Directory.GetFiles(directoryPath));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving entries from directory {directoryPath}: {ex.Message}");
            entries.Add($"!Error retrieving entries from directory {directoryPath}: {ex.Message}");
        }

        return entries;
    }

    private static async Task SendMessagesAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await Console.In.ReadLineAsync();
                Debug.Assert(message != null, nameof(message) + " != null");
                var data = Encoding.UTF8.GetBytes(message);
                await client.GetStream().WriteAsync(data, cancellationToken);

                await Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Send messages canceled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private static void CloseConnection(TcpClient client)
    {
        client.Close();
        Console.WriteLine("FreeClient disconnected.");
    }
}