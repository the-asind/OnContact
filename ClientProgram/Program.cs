using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClientProgram;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var serverIp = IPAddress.Loopback.ToString();
        using var client = new TcpClient();
        try
        {
            await ConnectToServerAsync(client, serverIp);
            Console.WriteLine("Server found.");
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var message = GetMessageFromUser();
                    await SendMessageAsync(client, message);
                }

                var response = await ReceiveMessageAsync(client);
                if (!string.IsNullOrEmpty(response)) DisplayServerResponse(response);

                // Add any other processing or tasks that need to be performed concurrently

                await Task.Delay(100); // Add a small delay to avoid high CPU usage
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

    private static async Task ConnectToServerAsync(TcpClient client, string serverIP)
    {
        await client.ConnectAsync(serverIP, 29000);
    }

    private static string GetMessageFromUser()
    {
        return Console.ReadLine();
    }

    private static async Task SendMessageAsync(TcpClient client, string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await client.GetStream().WriteAsync(data, 0, data.Length);
    }

    private static async Task<string> ReceiveMessageAsync(TcpClient client)
    {
        var data = new byte[256];
        var response = new StringBuilder();
        if (client.GetStream().DataAvailable)
        {
            var bytes = await client.GetStream().ReadAsync(data, 0, data.Length);
            response.Append(Encoding.UTF8.GetString(data, 0, bytes));
        }

        return response.ToString();
    }

    private static void DisplayServerResponse(string response)
    {
        Console.WriteLine("Server response: " + response);
    }

    private static void CloseConnection(TcpClient client)
    {
        client.Close();
    }

    private static void HandleException(Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}