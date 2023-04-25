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

    public async Task<byte[]> ReceiveMessageAsync()
    {
        var data = new byte[1024];
        var response = new MemoryStream();
        while (true)
        {
            if (_client.GetStream().DataAvailable)
            {
                var bytes = await _client.GetStream().ReadAsync(data);
                await response.WriteAsync(data, 0, bytes);
                if (!_client.GetStream().DataAvailable) break;
            }

            await Task.Delay(1); // Wait for 1ms before checking for data again
        }

        return response.ToArray();
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