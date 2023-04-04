using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerProgram
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                IPAddress localAddr = IPAddress.Parse(IPAddress.Loopback.ToString());
                TcpListener server = new TcpListener(localAddr, 29000);
                server.Start();
                Console.WriteLine("Server started.");

                while (true)
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    Console.WriteLine("Client connected.");

                    try
                    {
                        _ = Task.Run(() => ReceiveMessagesAsync(client));
                        _ = Task.Run(() => SendMessagesAsync(client));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                server.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.HelpLink);
            }
            Console.ReadKey();
        }

        static async Task ReceiveMessagesAsync(TcpClient client)
        {
            try
            {
                while (true)
                {
                    byte[] data = new byte[256];
                    int bytes = await client.GetStream().ReadAsync(data, 0, data.Length);
                    string message = Encoding.UTF8.GetString(data, 0, bytes);
                    Console.WriteLine("Received message: " + message);
                }
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

        static async Task SendMessagesAsync(TcpClient client)
        {
            try
            {
                while (true)
                {
                    Console.Write("Enter a message to send to the client: ");
                    string message = Console.ReadLine();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    await client.GetStream().WriteAsync(data, 0, data.Length);
                }
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

        static void CloseConnection(TcpClient client)
        {
            client.Close();
            Console.WriteLine("Client disconnected.");
        }
    }
}
