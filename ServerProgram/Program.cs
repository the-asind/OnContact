using System.Net;
using System.Net.Sockets;
using System.Text;
using System;

namespace ServerProgram;

internal class Program
{
    private static async Task Main()
    {
        try
        {
            var localAddr = IPAddress.Parse(IPAddress.Loopback.ToString());
            var listener = new TcpListener(localAddr, 29000);
            listener.ExclusiveAddressUse = true;
            listener.Start();

            Console.WriteLine("Server started.");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                //var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Client connected.");
                
                CancellationTokenSource cts = new CancellationTokenSource();
                
                Task receive = null;
                Task send = null;

                try
                {
                    send = Task.Run(() => SendMessagesAsync(client, cts.Token), cts.Token);
                    receive = Task.Run(() => ReceiveMessagesAsync(client, cts.Token), cts.Token);
                    
                    await Task.WhenAny(send, receive);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + "ФЛЫВОЛДФЫОВ");
                }
                finally
                {
                    Console.WriteLine("сработал нахуй");
                    cts.Cancel();
                    await Task.WhenAll(send, receive).ContinueWith(_ => client.Close());
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
                
                await Task.Delay(100);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Receive messages canceled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message + "БЕБРА");
        }
        finally
        {
            CloseConnection(client);
        }
    }

    private static async Task SendMessagesAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await Console.In.ReadLineAsync();
                var data = Encoding.UTF8.GetBytes(message);
                await client.GetStream().WriteAsync(data, cancellationToken);
                
                await Task.Delay(100);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Send messages canceled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message + "МЕМРА");
        }
    }

    private static void CloseConnection(TcpClient client)
    {
        client.Close();
        Console.WriteLine("Client disconnected.");
    }
}