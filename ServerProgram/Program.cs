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

        Task? receive = null;

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

            
            receive = Run(() => ReceiveMessagesAsync(client, newPort, cts.Token), cts.Token);

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

                var answer = await cancellationToken.GetAnswer(message);
                Debug.Assert(answer != null, nameof(answer) + " != null");
                var data = Encoding.UTF8.GetBytes(answer);
                await client.GetStream().WriteAsync(data, cancellationToken);

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
    
    private static async Task<string?> GetAnswer(this CancellationToken cancellationToken, string message)
    {
        string? answer = null;
        if (message.EndsWith(".txt"))
        {
            if (File.Exists(message))
                answer = await GetTxtContent(cancellationToken, message);
            else if (Directory.Exists(message)) 
                answer = GetListFiles(message);
        }
        else if (Directory.Exists(message)) 
            answer = GetListFiles(message);

        if (answer is null)
        {
            Console.WriteLine($"send file or directory {message} does not exist");
            answer = $"!File or directory {message} does not exist";
        }

        if (answer == "")
            answer = "!This directory is empty.";

        return answer;
    }

    private static string GetListFiles(string message)
    {
        var entries = GetAllDirectoryEntries(message);
        var answerBuilder = new StringBuilder();
        foreach (var entry in entries)
        {
            answerBuilder.AppendLine(entry);
        }

        var answer = "!"+answerBuilder;
        return answer;
    }

    private static async Task<string?> GetTxtContent(CancellationToken cancellationToken, string message)
    {
        await using var fileStream = new FileStream(message, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var streamReader = new StreamReader(fileStream);
        var buffer = new char[4096];
        var stringBuilder = new StringBuilder();
        var chunkSize = 1024 * 1024 / 4; // 0.25MB chunk size
        
        long fileSize = streamReader.BaseStream.Length;
        if (fileSize > chunkSize)
        {
            var answer = "!Contents of {message}:\n\n";
            return answer;
        }
        while (!streamReader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readCount = await streamReader.ReadAsync(buffer, 0, buffer.Length);
            stringBuilder.Append(buffer, 0, readCount);

            if (stringBuilder.Length >= chunkSize)
            {
                var chunk = stringBuilder.ToString(0, chunkSize);
                stringBuilder.Remove(0, chunkSize);

                Console.WriteLine($"sending partial contents of {message}");
                return chunk;
            }
        }

        if (stringBuilder.Length > 0)
        {
            var answer = $"!Contents of {message}:\n\n{stringBuilder}";
            Console.WriteLine($"sending contents of {message}");
            return answer;
        }

        return null;
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