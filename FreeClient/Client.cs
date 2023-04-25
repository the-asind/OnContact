using System.Net.Sockets;
using System.Text;

namespace FreeClient;

public class Client
{
    private TcpClient _client;

    public Client(TcpClient client)
    {
        _client = client;
    }

    public async Task ConnectAsync(string serverIp)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(serverIp, 29000);
    }

    public async Task SendMessageAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await _client.GetStream().WriteAsync(data);
    }

    public async Task<string> ReceiveMessageAsync()
    {
        var data = new byte[1024];
        var response = new StringBuilder();
        while (true)
        {
            if (_client.GetStream().DataAvailable)
            {
                var bytes = await _client.GetStream().ReadAsync(data);
                response.Append(Encoding.UTF8.GetString(data, 0, bytes));
                break;
            }

            await Task.Delay(1); // Wait for 100ms before checking for data again
        }

        return response.ToString();
    }

    public void CloseConnection()
    {
        _client.Close();
    }

    public static void HandleException(Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }
}