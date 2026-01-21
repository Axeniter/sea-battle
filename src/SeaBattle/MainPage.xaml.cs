using SeaBattle.Models;
using SeaBattle.Services;
using System.Collections.ObjectModel;
using SeaBattle.Server;

namespace SeaBattle
{
    public partial class MainPage : ContentPage
    {
        private GameServer p2pServer;
        private GameEngine gameEngine;
        private BoardService boardManager;
        private NetworkMessageHandler messageHandler;
        private AttackHandler attackHandler;

        private ObservableCollection<Room> availableServers = new ObservableCollection<Room>();
        private Dictionary<string, DateTime> serverLastSeen = new Dictionary<string, DateTime>();

        public bool IsGameOver { get; private set; }
        private bool isServerMode = false;

        /// <summary>
        /// Main page constructor
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            p2pServer = new GameServer("Player");
            gameEngine = new GameEngine();

            boardManager = new BoardService(gameEngine, PlayerGrid, EnemyGrid, OnPlayerGridClicked, OnEnemyGridClicked);

            messageHandler = new NetworkMessageHandler(
                gameEngine, p2pServer,
                () => IsGameOver,
                UpdateGameStatus,
                EndGame,
                CheckEnemyWinCondition,
                ProcessEnemySpecialAttack
            );

            attackHandler = new AttackHandler(
                gameEngine, p2pServer,
                UpdateBoardDisplay,
                UpdateSpecialAttacksUI,
                EndGame,
                CheckEnemyWinCondition,
                UpdateGameStatus
            );

            SetupEventHandlers();
            StartDiscovery();
            _ = ServerCleaner();

            // Initialize UI
            SpecialAttacksContainer.IsVisible = false;
            ShipPlacementContainer.IsVisible = true;
        }

        /// <summary>
        /// Sets up event handlers
        /// </summary>
        private void SetupEventHandlers()
        {
            p2pServer.client.MessageReceived += messageHandler.OnMessageReceived;
            p2pServer.server.MessageReceived += messageHandler.OnMessageReceived;
            p2pServer.server.ClientConnected += OnClientConnected;
            p2pServer.discovery.ServerFound += OnServerFound;

            gameEngine.GameStateChanged += OnGameStateChanged;
            gameEngine.BoardUpdated += OnBoardUpdated;
            gameEngine.SpecialAttacksUpdated += OnSpecialAttacksUpdated;
        }

        /// <summary>
        /// Starts server discovery
        /// </summary>
        private void StartDiscovery()
        {
            try
            {
                p2pServer.discovery.Stop();
                p2pServer.discovery.StartListening();
                p2pServer.isListeningBroadcast = true;
                StatusLabel.Text = "Searching for rooms...";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromArgb("#f59e0b"));
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Cleans up old servers from the list
        /// </summary>
        private async Task ServerCleaner()
        {
            while (true)
            {
                await Task.Delay(3000);

                if (p2pServer.isListeningBroadcast && !isServerMode)
                {
                    CleanOldServers();
                }
            }
        }

        /// <summary>
        /// Removes unresponsive servers
        /// </summary>
        private void CleanOldServers()
        {
            var now = DateTime.Now;
            var serversToRemove = new List<string>();

            foreach (var serverIp in serverLastSeen.Keys.ToList())
            {
                var lastSeen = serverLastSeen[serverIp];
                if ((now - lastSeen).TotalSeconds > 5)
                {
                    serversToRemove.Add(serverIp);
                }
            }

            foreach (var serverIp in serversToRemove)
            {
                serverLastSeen.Remove(serverIp);
                var serverToRemove = availableServers.FirstOrDefault(s => s.IP == serverIp);
                if (serverToRemove != null)
                {
                    availableServers.Remove(serverToRemove);
                }
            }
        }

        /// <summary>
        /// Initializes a new game
        /// </summary>
        private void InitializeGame()
        {
            gameEngine.ResetGame();
            boardManager.CreateGameBoards();
            UpdateShipsProgress();
            UpdateSpecialAttacksUI();
            GameStatusLabel.Text = "Click on your board to place ships. Use Rotate to change direction.";
            RotateShipBtn.IsEnabled = true;
            IsGameOver = false;

            SpecialAttacksContainer.IsVisible = false;
            ShipPlacementContainer.IsVisible = true;
            GameControlTitle.Text = "SHIP DEPLOYMENT";
            GameControlSubtitle.Text = "Place all ships to start battle";
        }

        /// <summary>
        /// Updates the game status on the UI
        /// </summary>
        /// <param name="status">Status text</param>
        public void UpdateGameStatus(string status)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                GameStatusLabel.Text = status;
            });
        }

        /// <summary>
        /// Updates the display of game boards
        /// </summary>
        public void UpdateBoardDisplay()
        {
            boardManager.UpdateBoardDisplay();
        }

        /// <summary>
        /// Checks enemy win condition
        /// </summary>
        /// <returns>True if enemy won, otherwise False</returns>
        public bool CheckEnemyWinCondition()
        {
            int hitCount = 0;
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    if (gameEngine.EnemyBoard[x, y] == CellState.Hit)
                    {
                        hitCount++;
                    }
                }
            }
            return hitCount >= 10;
        }

        /// <summary>
        /// Ends the game
        /// </summary>
        /// <param name="isWinner">True if player won, False if lost</param>
        public void EndGame(bool isWinner)
        {
            IsGameOver = true;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (isWinner)
                {
                    await DisplayAlert("Game Over", "You won! All enemy ships are sunk.", "OK");
                    GameStatusLabel.Text = "Game Over - You won";
                }
                else
                {
                    await DisplayAlert("Game Over", "You lost! All your ships are sunk.", "OK");
                    GameStatusLabel.Text = "Game Over - You lost";
                }

                SpecialAttacksContainer.IsVisible = false;
                ShipPlacementContainer.IsVisible = false;
                GameControlTitle.Text = "GAME OVER";
                GameControlSubtitle.Text = isWinner ? "You are victorious" : "Better luck next time";
            });
        }

        /// <summary>
        /// Updates ship placement progress
        /// </summary>
        private void UpdateShipsProgress()
        {
            OrientationLabel.Text = gameEngine.IsShipHorizontal ? "Horizontal" : "Vertical";
        }

        /// <summary>
        /// Updates special attacks UI state
        /// </summary>
        public void UpdateSpecialAttacksUI()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update button states
                LineHorizontalBtn.IsEnabled = gameEngine.HasLineHorizontalAttack;
                LineVerticalBtn.IsEnabled = gameEngine.HasLineVerticalAttack;

                // Update Border background colors
                UpdateButtonBackground(LineHorizontalBtn, gameEngine.HasLineHorizontalAttack ? "#10b981" : "#cbd5e0");
                UpdateButtonBackground(LineVerticalBtn, gameEngine.HasLineVerticalAttack ? "#3b82f6" : "#cbd5e0");

                // Update status text
                if (gameEngine.SelectedSpecialAttack != AttackType.Standard)
                {
                    string attackName = gameEngine.SelectedSpecialAttack.ToString();
                    SpecialAttackStatusLabel.Text = $"{attackName} selected - click on enemy board";
                }
                else
                {
                    SpecialAttackStatusLabel.Text = "Select special attack type";
                }

                // Update selected attack highlight
                UpdateSpecialAttackSelection();
            });
        }

        /// <summary>
        /// Updates button background color
        /// </summary>
        private void UpdateButtonBackground(Button button, string colorHex)
        {
            if (button.Parent is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(colorHex));
            }
        }

        /// <summary>
        /// Highlights the selected special attack
        /// </summary>
        private void UpdateSpecialAttackSelection()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Reset all highlights
                ResetButtonSelection(LineHorizontalBtn);
                ResetButtonSelection(LineVerticalBtn);

                // Highlight selected attack
                switch (gameEngine.SelectedSpecialAttack)
                {
                    case AttackType.LineHorizontal:
                        SetButtonSelected(LineHorizontalBtn, "#059669");
                        break;
                    case AttackType.LineVertical:
                        SetButtonSelected(LineVerticalBtn, "#1d4ed8");
                        break;
                }
            });
        }

        /// <summary>
        /// Sets button as selected
        /// </summary>
        private void SetButtonSelected(Button button, string strokeColorHex)
        {
            if (button.Parent is Border border)
            {
                border.Stroke = new SolidColorBrush(Color.FromArgb(strokeColorHex));
                border.StrokeThickness = 2;
            }
        }

        /// <summary>
        /// Resets button selection
        /// </summary>
        private void ResetButtonSelection(Button button)
        {
            if (button.Parent is Border border)
            {
                border.Stroke = new SolidColorBrush(Colors.Transparent);
                border.StrokeThickness = 0;
            }
        }

        /// <summary>
        /// Processes enemy's special attack
        /// </summary>
        /// <param name="attackType">Attack type</param>
        /// <param name="startX">X coordinate</param>
        /// <param name="startY">Y coordinate</param>
        public async Task ProcessEnemySpecialAttack(string attackType, int startX, int startY)
        {
            await attackHandler.ProcessEnemySpecialAttack(attackType, startX, startY);
        }

        #region Event Handlers

        /// <summary>
        /// Handles start server button click
        /// </summary>
        private async void OnStartServerClicked(object sender, EventArgs e)
        {
            try
            {
                p2pServer.StartServer();
                isServerMode = true;
                serverLastSeen.Clear();
                availableServers.Clear();
                p2pServer.connectedClientIp = "";
                IsGameOver = false;

                StartServerBtn.IsEnabled = false;
                StopServerBtn.IsEnabled = true;
                DisconnectBtn.IsEnabled = true;
                StatusLabel.Text = "Room created";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromArgb("#22c55e"));
                await DisplayAlert("Room", "Room created successfully", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to start server: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Handles stop server button click
        /// </summary>
        private async void OnStopServerClicked(object sender, EventArgs e)
        {
            try
            {
                p2pServer.isServerMode = false;
                p2pServer.server.StopListening();
                p2pServer.discovery.Stop();
                isServerMode = false;
                p2pServer.connectedClientIp = "";
                IsGameOver = false;

                StartServerBtn.IsEnabled = true;
                StopServerBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = false;
                StatusLabel.Text = "Server stopped";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromArgb("#ef4444"));

                await Task.Delay(500);
                StartDiscovery();

                await DisplayAlert("Room", "Room closed", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to stop server: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Handles disconnect button click
        /// </summary>
        private async void OnDisconnectClicked(object sender, EventArgs e)
        {
            try
            {
                if (p2pServer.client.IsConnected)
                {
                    p2pServer.client = new TcpMessageClient();
                }

                p2pServer.connectedClientIp = "";
                IsGameOver = false;

                if (isServerMode)
                {
                    p2pServer.isServerMode = false;
                    p2pServer.server.StopListening();
                    isServerMode = false;
                }

                p2pServer.discovery.Stop();
                await Task.Delay(500);
                StartDiscovery();

                StartServerBtn.IsEnabled = true;
                StopServerBtn.IsEnabled = false;
                NetworkReadyBtn.IsEnabled = false;
                DisconnectBtn.IsEnabled = false;
                RotateShipBtn.IsEnabled = false;
                StatusLabel.Text = "Disconnected";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromArgb("#ef4444"));
                GameStatusLabel.Text = "Create room or connect to one";

                SpecialAttacksContainer.IsVisible = false;
                ShipPlacementContainer.IsVisible = false;
                GameControlTitle.Text = "DISCONNECTED";
                GameControlSubtitle.Text = "Connect to start game";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to disconnect: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Handles server selection from the list
        /// </summary>
        private async void OnServerSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is Room server)
            {
                StatusLabel.Text = $"Connecting to {server.Name}...";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromArgb("#f59e0b"));
                bool connected = await p2pServer.ConnectToServer(server.IP);

                if (connected)
                {
                    StatusLabel.Text = $"Connected to {server.Name}";
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromArgb("#22c55e"));
                    NetworkReadyBtn.IsEnabled = true;
                    DisconnectBtn.IsEnabled = true;
                    RotateShipBtn.IsEnabled = true;
                    IsGameOver = false;
                    InitializeGame();
                }
                else
                {
                    StatusLabel.Text = "Failed to connect";
                    StatusIndicator.Fill = new SolidColorBrush(Color.FromArgb("#ef4444"));
                    await DisplayAlert("Error", "Failed to connect", "OK");
                    StartDiscovery();
                }

                ServersListView.SelectedItem = null;
            }
        }

        /// <summary>
        /// Handles new server discovery
        /// </summary>
        private void OnServerFound(string ip, string name)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (isServerMode) return;

                serverLastSeen[ip] = DateTime.Now;

                var existingServer = availableServers.FirstOrDefault(s => s.IP == ip);
                if (existingServer == null)
                {
                    availableServers.Add(new Room { IP = ip, Name = name });
                    ServersListView.ItemsSource = availableServers.ToList();
                }
                else
                {
                    existingServer.Name = name;
                }

                StatusLabel.Text = $"Found {availableServers.Count} server(s)";
            });
        }

        /// <summary>
        /// Handles client connection to the server
        /// </summary>
        private void OnClientConnected(string clientIp)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!isServerMode) return;

                p2pServer.connectedClientIp = clientIp;
                StatusLabel.Text = $"Client connected: {clientIp}";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromArgb("#22c55e"));
                NetworkReadyBtn.IsEnabled = true;
                DisconnectBtn.IsEnabled = true;
                RotateShipBtn.IsEnabled = true;
                IsGameOver = false;
                InitializeGame();
            });
        }

        /// <summary>
        /// Handles click on player grid
        /// </summary>
        private void OnPlayerGridClicked(int x, int y)
        {
            if (IsGameOver) return;

            if (gameEngine.CurrentState == GameState.ShipPlacement)
            {
                bool placed = gameEngine.PlaceShip(x, y);
                if (placed)
                {
                    UpdateBoardDisplay();
                    UpdateShipsProgress();

                    if (gameEngine.AllShipsPlaced())
                    {
                        GameStatusLabel.Text = "All ships placed! Click Ready when done.";
                        RotateShipBtn.IsEnabled = false;
                        ReadyGameBtn.IsEnabled = true;
                    }
                    else
                    {
                        GameStatusLabel.Text = $"Ship placed! Place {gameEngine.TotalShipsCount - gameEngine.PlacedShipsCount} more ships.";
                    }
                }
                else
                {
                    DisplayAlert("Cannot Place Ship", "Place ship on correct position", "OK");
                }
            }
        }

        /// <summary>
        /// Handles click on enemy grid
        /// </summary>
        private async void OnEnemyGridClicked(int x, int y)
        {
            if (IsGameOver) return;

            if (gameEngine.CurrentState == GameState.Player1Turn && gameEngine.IsPlayerTurn)
            {
                if (gameEngine.SelectedSpecialAttack != AttackType.Standard)
                {
                    await attackHandler.ExecuteSpecialAttack(x, y);
                }
                else if (gameEngine.EnemyBoard[x, y] == CellState.Empty)
                {
                    await p2pServer.SendMessage($"SHOT:{x}:{y}");
                    GameStatusLabel.Text = "Attack sent! Waiting for enemy...";
                }
            }
        }

        /// <summary>
        /// Handles rotate ship button click
        /// </summary>
        private void OnRotateShipClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;
            gameEngine.RotateShip();
            UpdateShipsProgress();
        }

        /// <summary>
        /// Handles ready button click (in game panel)
        /// </summary>
        private async void OnReadyGameClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;

            if (gameEngine.CurrentState == GameState.ShipPlacement && gameEngine.AllShipsPlaced())
            {
                gameEngine.SetPlayerReady(true);
                await p2pServer.SendMessage("READY");

                if (gameEngine.IsEnemyReady)
                {
                    gameEngine.StartGame();
                }
                else
                {
                    GameStatusLabel.Text = "Waiting for enemy to ready up...";
                    GameControlSubtitle.Text = "Waiting for opponent...";
                }
            }
            else if (!gameEngine.AllShipsPlaced())
            {
                await DisplayAlert("Not Ready", $"Place all ships first! {gameEngine.TotalShipsCount - gameEngine.PlacedShipsCount} ships remaining.", "OK");
            }
        }

        /// <summary>
        /// Handles ready button click (in network panel)
        /// </summary>
        private async void OnNetworkReadyClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;

            if (gameEngine.CurrentState == GameState.ShipPlacement && gameEngine.AllShipsPlaced())
            {
                gameEngine.SetPlayerReady(true);
                await p2pServer.SendMessage("READY");

                if (gameEngine.IsEnemyReady)
                {
                    gameEngine.StartGame();
                }
                else
                {
                    GameStatusLabel.Text = "Waiting for enemy to ready up...";
                    GameControlSubtitle.Text = "Waiting for opponent...";
                }
            }
            else if (!gameEngine.AllShipsPlaced())
            {
                await DisplayAlert("Not Ready", $"Place all ships first! {gameEngine.TotalShipsCount - gameEngine.PlacedShipsCount} ships remaining.", "OK");
            }
        }

        /// <summary>
        /// Handles horizontal line attack selection
        /// </summary>
        private void OnLineHorizontalClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;
            gameEngine.SelectSpecialAttack(AttackType.LineHorizontal);
            SpecialAttackStatusLabel.Text = "Horizontal Line selected - click on enemy board";
            UpdateSpecialAttackSelection();
        }

        /// <summary>
        /// Handles vertical line attack selection
        /// </summary>
        private void OnLineVerticalClicked(object sender, EventArgs e)
        {
            if (IsGameOver) return;
            gameEngine.SelectSpecialAttack(AttackType.LineVertical);
            SpecialAttackStatusLabel.Text = "Vertical Line selected - click on enemy board";
            UpdateSpecialAttackSelection();
        }

        /// <summary>
        /// Handles game state changes
        /// </summary>
        private void OnGameStateChanged(GameState newState)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (IsGameOver) return;

                switch (newState)
                {
                    case GameState.ShipPlacement:
                        GameStatusLabel.Text = "Place your ships - click on your board";
                        RotateShipBtn.IsEnabled = true;
                        ReadyGameBtn.IsEnabled = true;
                        NetworkReadyBtn.IsEnabled = true;
                        SpecialAttacksContainer.IsVisible = false;
                        ShipPlacementContainer.IsVisible = true;
                        GameControlTitle.Text = "SHIP DEPLOYMENT";
                        GameControlSubtitle.Text = "Place all ships to start battle";
                        break;
                    case GameState.Player1Turn:
                        GameStatusLabel.Text = "Your turn - attack enemy ships!";
                        RotateShipBtn.IsEnabled = false;
                        ReadyGameBtn.IsEnabled = false;
                        NetworkReadyBtn.IsEnabled = false;
                        SpecialAttacksContainer.IsVisible = true;
                        ShipPlacementContainer.IsVisible = false;
                        GameControlTitle.Text = "SPECIAL ATTACKS";
                        GameControlSubtitle.Text = "Select attack type and click enemy board";
                        UpdateSpecialAttacksUI();
                        break;
                    case GameState.Player2Turn:
                        GameStatusLabel.Text = "Enemy's turn - waiting...";
                        RotateShipBtn.IsEnabled = false;
                        ReadyGameBtn.IsEnabled = false;
                        NetworkReadyBtn.IsEnabled = false;
                        SpecialAttacksContainer.IsVisible = true;
                        ShipPlacementContainer.IsVisible = false;
                        GameControlTitle.Text = "SPECIAL ATTACKS";
                        GameControlSubtitle.Text = "Enemy is attacking...";
                        break;
                }
            });
        }

        /// <summary>
        /// Handles board updates
        /// </summary>
        private void OnBoardUpdated()
        {
            UpdateBoardDisplay();
        }

        /// <summary>
        /// Handles special attacks updates
        /// </summary>
        private void OnSpecialAttacksUpdated()
        {
            UpdateSpecialAttacksUI();
        }

        #endregion
    }
}