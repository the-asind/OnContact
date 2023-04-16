using System.Net.Sockets;
using System.Text;

namespace ClientProgram;

public class TcpClientPlugin
{
    public delegate void MessageReceivedHandler(string message);

    public event MessageReceivedHandler MessageReceived;

    public delegate string GetMessageHandler();

    public GetMessageHandler GetMessage;

    public delegate void LogMessageHandler(string message);

    public event LogMessageHandler LogMessage;

    private readonly TcpClient _client;

    public TcpClientPlugin()
    {
        _client = new TcpClient();
    }

    public async Task ConnectToServerAsync(string serverIP, int port)
    {
        await _client.ConnectAsync(serverIP, port);
        LogMessage?.Invoke("Connected to server.");
    }

    public async Task StartAsync()
    {
        try
        {
            while (true)
            {
                if (GetMessage != null)
                {
                    var message = GetMessage();
                    if (!string.IsNullOrEmpty(message)) await SendMessageAsync(message);
                }

                var response = await ReceiveMessageAsync();
                if (!string.IsNullOrEmpty(response))
                {
                    MessageReceived?.Invoke(response);
                    LogMessage?.Invoke("Server response: " + response);
                }

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
            CloseConnection();
        }
    }

    private async Task SendMessageAsync(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await _client.GetStream().WriteAsync(data, 0, data.Length);
    }

    private async Task<string> ReceiveMessageAsync()
    {
        var data = new byte[256];
        var response = new StringBuilder();
        if (_client.GetStream().DataAvailable)
        {
            var bytes = await _client.GetStream().ReadAsync(data, 0, data.Length);
            response.Append(Encoding.UTF8.GetString(data, 0, bytes));
        }

        return response.ToString();
    }

    private void CloseConnection()
    {
        _client.Close();
    }

    private void HandleException(Exception ex)
    {
        LogMessage?.Invoke(ex.ToString());
    }
}