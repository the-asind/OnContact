using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Threading.Tasks.Task;

namespace ServerProgram;

internal class Program
{
    private static async Task Main()
    {
        try
        {
            var localAddr = IPAddress.Parse(IPAddress.Loopback.ToString());
            // TODO: add free port finder
            var listener = new TcpListener(localAddr, 29000)
            {
                ExclusiveAddressUse = true
            };
            listener.Start();

            Console.WriteLine("Server started.");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                Console.WriteLine("Client connected.");

                var cts = new CancellationTokenSource();

                Task? receive = null;
                Task? send = null;

                try
                {
                    send = Run(() => SendMessagesAsync(client, cts.Token), cts.Token);
                    receive = Run(() => ReceiveMessagesAsync(client, cts.Token), cts.Token);

                    await WhenAny(send, receive);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    cts.Cancel();
                    Debug.Assert(send != null, nameof(send) + " != null");
                    Debug.Assert(receive != null, nameof(receive) + " != null");
                    await WhenAll(send, receive).ContinueWith(_ => client.Close());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.HelpLink);
        }

        Console.ReadKey();
    }

    private static async Task ReceiveMessagesAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[1024];
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await client.GetStream().ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received message: {message}");

                var answer = await GetAnswer(cancellationToken, message);
                Debug.Assert(answer != null, nameof(answer) + " != null");
                var data = Encoding.UTF8.GetBytes(answer);
                await client.GetStream().WriteAsync(data, cancellationToken);

                await Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Receive messages canceled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            CloseConnection(client);
        }
    }

    private static async Task<string?> GetAnswer(CancellationToken cancellationToken, string message)
    {
        string? answer = null;
        if (message.EndsWith(".txt"))
        {
            if (File.Exists(message))
            {
                answer = await GetTxtContent(cancellationToken, message);
            }
            else
            {
                if (Directory.Exists(message))
                {
                    answer = GetListFiles(message);
                }
            }
        }
        else if (Directory.Exists(message))
        {
            answer = GetListFiles(message);
        }
        
        if (answer is null)
        {
            Console.WriteLine($"send file or directory {message} does not exist");
            answer = $"File or directory {message} does not exist";
        }

        return answer;
    }

    private static string? GetListFiles(string message)
    {
        var entries = GetAllDirectoryEntries(message);
        StringBuilder answerBuilder = new StringBuilder();
        foreach (string entry in entries)
        {
            answerBuilder.AppendLine(entry);
        }

        var answer = answerBuilder.ToString();
        return answer;
    }

    private static async Task<string?> GetTxtContent(CancellationToken cancellationToken, string message)
    {
        var fileContents = await File.ReadAllTextAsync(message, cancellationToken);
        Console.WriteLine($"send contents of {message}: {fileContents}");
        var answer = $"Contents of {message}: {fileContents}";
        return answer;
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
            entries.Add($"Error retrieving entries from directory {directoryPath}: {ex.Message}");
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
        Console.WriteLine("Client disconnected.");
    }
}