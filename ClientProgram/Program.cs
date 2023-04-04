using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ClientProgram
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serverIP = IPAddress.Loopback.ToString();
            using var client = new TcpClient();
            try
            {
                await ConnectToServerAsync(client, serverIP);

                while (true)
                {
                    var message = GetMessageFromUser();
                    await SendMessageAsync(client, message);

                    var response = await ReceiveMessageAsync(client);
                    DisplayServerResponse(response);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {
                CloseConnection(client);
            }

            Console.ReadKey();
        }

        static async Task ConnectToServerAsync(TcpClient client, string serverIP)
        {
            await client.ConnectAsync(serverIP, 29000);
        }

        static string GetMessageFromUser()
        {
            Console.Write("Enter a message to send to the server: ");
            return Console.ReadLine();
        }

        static async Task SendMessageAsync(TcpClient client, string message)
        {
            var data = Encoding.UTF8.GetBytes(message);
            await client.GetStream().WriteAsync(data, 0, data.Length);
        }

        static async Task<string> ReceiveMessageAsync(TcpClient client)
        {
            var data = new byte[256];
            var response = new StringBuilder();
            var bytes = await client.GetStream().ReadAsync(data, 0, data.Length);
            response.Append(Encoding.ASCII.GetString(data, 0, bytes));
            return response.ToString();
        }

        static void DisplayServerResponse(string response)
        {
            Console.WriteLine("Server response: " + response);
        }

        static void CloseConnection(TcpClient client)
        {
            client.Close();
        }

        static void HandleException(Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}
