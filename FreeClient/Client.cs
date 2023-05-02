using System.Diagnostics.CodeAnalysis;
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

    [SuppressMessage("ReSharper.DPA", "DPA0001: Memory allocation issues")]
    public async Task<byte[]> ReceiveMessageAsync(int chunkSize, bool getWholeStream = true)
    {
        //Check if the data is received correctly - TCP confirming the data is received correctly
        var data = new byte[chunkSize];
        var response = new MemoryStream();
        while (true)
        {
            if (_client.GetStream().DataAvailable)
            {
                var bytes = await _client.GetStream().ReadAsync(data);
                await response.WriteAsync(data.AsMemory(0, bytes));
                if (getWholeStream)
                    if (!_client.GetStream().DataAvailable) break;
                    else
                        break;
            }

            await Task.Delay(50); // Wait for 1ms before checking for data again
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