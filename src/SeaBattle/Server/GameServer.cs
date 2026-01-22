namespace SeaBattle.Server
{
    /// <summary>
    /// Implements game server
    /// </summary>
    public class GameServer
    {
        public TcpMessageClient client;
        public TcpMessageServer server;
        public UdpDiscovery discovery;
        public bool isServerMode = false;
        public string connectedClientIp = "";
        public bool isBroadcasting = false;
        public bool isListeningBroadcast = false;
        public string username = "user";

        /// <summary>
        /// Initializes a new instance of the GameServer class
        /// </summary>
        public GameServer(string name)
        {
            client = new TcpMessageClient();
            server = new TcpMessageServer();
            discovery = new UdpDiscovery();
            username = name;
        }

        /// <summary>
        /// Starts the game server in server mode
        /// </summary>
        public async void StartServer()
        {
            try
            {
                if (isServerMode)
                {
                    return;
                }

                StopDiscovery();
                isServerMode = true;
                isBroadcasting = true;
                server.StartListeningAsync();
                discovery.StartBroadcasting(username);
            }
            catch (Exception)
            {
                return;
            }
        }

        /// <summary>
        /// Connects to a remote game server
        /// </summary>
        public async Task<bool> ConnectToServer(string IP)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(IP))
                {
                    return false;
                }

                isListeningBroadcast = false;
                StopDiscovery();

                if (isServerMode)
                {
                    isServerMode = false;
                    server.StopListening();
                    await Task.Delay(100);
                }

                string targetIp = IP.Trim();
                bool connected = await client.ConnectToServer(targetIp, 8080);

                if (!connected)
                {
                    isListeningBroadcast = true;
                    discovery.Stop();
                    await Task.Delay(100);
                    discovery.StartListening();
                }

                return connected;
            }
            catch (Exception)
            {
                isListeningBroadcast = true;
                discovery.Stop();
                await Task.Delay(100);
                discovery.StartListening();
                return false;
            }
        }

        /// <summary>
        /// Sends a message to the connected client or server
        /// </summary>
        public async Task<bool> SendMessage(string Text)
        {
            try
            {
                if (string.IsNullOrEmpty(Text))
                {
                    return false;
                }

                if (client.IsConnected)
                {
                    bool sent = await client.SendMessageAsync(Text.Trim());
                    return sent;
                }
                else if (isServerMode && !string.IsNullOrEmpty(connectedClientIp))
                {
                    bool sent = await server.SendMessageToClient(connectedClientIp, Text.Trim());
                    return sent;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Stops all server discovery activities
        /// </summary>
        private void StopDiscovery()
        {
            discovery.Stop();
            isBroadcasting = false;
            isListeningBroadcast = false;
        }
    }
}
