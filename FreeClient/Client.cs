using System.Net.Sockets;
using System.Text;

namespace FreeClient;

public class Client
{
    private TcpClient client;

    public async Task ConnectAsync(string serverIP)
    {
        client = new TcpClient();
        await client.ConnectAsync(serverIP, 29000);
    }

    public async Task SendMessageAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await client.GetStream().WriteAsync(data, 0, data.Length);
    }

    public async Task<string> ReceiveMessageAsync()
    {
        var data = new byte[1024];
        var response = new StringBuilder();
        while (true)
        {
            if (client.GetStream().DataAvailable)
            {
                var bytes = await client.GetStream().ReadAsync(data, 0, data.Length);
                response.Append(Encoding.UTF8.GetString(data, 0, bytes));
                break;
            }
            await Task.Delay(100); // Wait for 100ms before checking for data again
        }
        return response.ToString();
    }


    public void CloseConnection()
    {
        client.Close();
    }

    public static void HandleException(Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}