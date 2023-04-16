using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    internal class Server
    {
        private static Func<string>? _getInputFromUI;

        public delegate void MessageReceivedEventHandler(string message);

        public static event MessageReceivedEventHandler MessageReceived;

        public delegate void ClientConnectedEventHandler();

        public static event ClientConnectedEventHandler ClientConnected;

        public delegate void ClientDisconnectedEventHandler();

        public static event ClientDisconnectedEventHandler ClientDisconnected;

        public static async Task Main(Func<string>? getInputFromUI)
        {
            _getInputFromUI = getInputFromUI;

            try
            {
                var localAddr = IPAddress.Parse(IPAddress.Loopback.ToString());
                var listener = new TcpListener(localAddr, 29000)
                {
                    ExclusiveAddressUse = true
                };

                listener.Start();
                OnMessageReceived("Server started.");

                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    OnClientConnected();

                    var cts = new CancellationTokenSource();
                    Task? receive = null;
                    Task? send = null;

                    try
                    {
                        send = Task.Run(() => SendMessagesAsync(client, cts.Token, _getInputFromUI), cts.Token);
                        receive = Task.Run(() => ReceiveMessagesAsync(client, cts.Token), cts.Token);

                        await Task.WhenAny(send, receive);
                    }
                    catch (Exception ex)
                    {
                        OnMessageReceived(ex.Message);
                    }
                    finally
                    {
                        cts.Cancel();
                        await Task.WhenAll(send!, receive!).ContinueWith(_ => CloseConnection(client));
                    }
                }
            }
            catch (Exception ex)
            {
                OnMessageReceived(ex.HelpLink);
            }
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
                    OnMessageReceived(message);

                    await Task.Delay(100);
                }
            }
            catch (OperationCanceledException)
            {
                OnMessageReceived("Receive messages canceled.");
            }
            catch (Exception ex)
            {
                OnMessageReceived(ex.Message);
            }
            finally
            {
                CloseConnection(client);
            }
        }

        private static async Task SendMessagesAsync(TcpClient client, CancellationToken cancellationToken,
            Func<string>? getInput)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = getInput!();
                    var data = Encoding.UTF8.GetBytes(message);
                    await client.GetStream().WriteAsync(data, cancellationToken);

                    await Task.Delay(100);
                }
            }
            catch (OperationCanceledException)
            {
                OnMessageReceived("Send messages canceled.");
            }
            catch (Exception ex)
            {
                OnMessageReceived(ex.Message);
            }
        }

        private static void CloseConnection(TcpClient client)
        {
            client.Close();
            OnClientDisconnected();
        }

        private static void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(message);
        }

        private static void OnClientConnected()
        {
            ClientConnected?.Invoke();
        }

        private static void OnClientDisconnected()
        {
            ClientDisconnected?.Invoke();
        }
    }
}