using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SeaBattle.Server
{
    /// <summary>
    /// TCP client for sending and receiving messages
    /// </summary>
    public class TcpMessageClient
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;

        public event Action<string, string> MessageReceived;
        private bool is_reading = false;

        public bool IsConnected => client?.Connected == true;

        /// <summary>
        /// Connects to a TCP server
        /// </summary>
        public async Task<bool> ConnectToServer(string targetIp, int port)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(targetIp, port);
                var stream = client.GetStream();
                writer = new StreamWriter(stream, Encoding.UTF8);
                reader = new StreamReader(stream, Encoding.UTF8);

                StartReading();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Sends a message to the connected server
        /// </summary>
        public async Task<bool> SendMessageAsync(string message)
        {
            try
            {
                if (!IsConnected || writer == null)
                    return false;

                await writer.WriteLineAsync(message);
                await writer.FlushAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void StartReading()
        {
            if (is_reading) return;
            is_reading = true;

            try
            {
                while (IsConnected && is_reading)
                {
                    string message = await reader.ReadLineAsync();
                    if (message == null) break;
                    string localIp = ((IPEndPoint)client.Client.LocalEndPoint).Address.ToString();
                    MessageReceived?.Invoke(localIp, message);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                is_reading = false;
            }
        }
    }
}
