using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SeaBattle.Server
{
    /// <summary>
    /// TCP server for accepting connections and handling messages
    /// </summary>
    public class TcpMessageServer
    {
        private int port = 8080;
        private bool is_listening = false;
        private TcpListener tcpListener;
        private Dictionary<string, TcpClient> connectedClients = new Dictionary<string, TcpClient>();

        public event Action<string, string> MessageReceived;
        public event Action<string> ClientConnected;

        /// <summary>
        /// Starts listening for incoming TCP connections
        /// </summary>
        public async Task StartListeningAsync()
        {
            if (is_listening) return;

            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            is_listening = true;

            while (is_listening)
            {
                try
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                    connectedClients[clientIp] = client;
                    ClientConnected?.Invoke(clientIp);

                    _ = Task.Run(() => HandleClientAsync(client, clientIp));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Stops listening and disconnects all clients
        /// </summary>
        public void StopListening()
        {
            is_listening = false;

            foreach (var client in connectedClients.Values)
            {
                client?.Close();
            }
            connectedClients.Clear();

            tcpListener?.Stop();
        }

        /// <summary>
        /// Sends a message to a specific connected client
        /// </summary>
        public async Task<bool> SendMessageToClient(string clientIp, string message)
        {
            try
            {
                if (connectedClients.TryGetValue(clientIp, out TcpClient client) && client.Connected)
                {
                    var stream = client.GetStream();
                    var writer = new StreamWriter(stream, Encoding.UTF8);

                    await writer.WriteLineAsync(message);
                    await writer.FlushAsync();
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task HandleClientAsync(TcpClient client, string clientIp)
        {
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (client.Connected && is_listening)
                    {
                        string message = await reader.ReadLineAsync();
                        if (message == null) break;
                        MessageReceived?.Invoke(clientIp, message);
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                connectedClients.Remove(clientIp);
                client?.Close();
            }
        }
    }
}

