using SeaBattle.Models;

namespace SeaBattle.Services
{
    /// <summary>
    /// Manages the creation and updating of game boards
    /// </summary>
    public class BoardService
    {
        private GameEngine gameEngine;
        private Grid playerGrid;
        private Grid enemyGrid;
        private Action<int, int> onPlayerGridClicked;
        private Action<int, int> onEnemyGridClicked;

        /// <summary>
        /// Initializes a new instance of the GameBoardManager class
        /// </summary>
        public GameBoardManager(GameEngine gameEngine, Grid playerGrid, Grid enemyGrid,
                              Action<int, int> onPlayerGridClicked, Action<int, int> onEnemyGridClicked)
        {
            this.gameEngine = gameEngine;
            this.playerGrid = playerGrid;
            this.enemyGrid = enemyGrid;
            this.onPlayerGridClicked = onPlayerGridClicked;
            this.onEnemyGridClicked = onEnemyGridClicked;
        }

        /// <summary>
        /// Creates the initial game boards
        /// </summary>
        public void CreateGameBoards()
        {
            CreateGrid(playerGrid, true);
            CreateGrid(enemyGrid, false);
        }

        /// <summary>
        /// Updates the visual display of both game boards
        /// </summary>
        public void UpdateBoardDisplay()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateGrid(playerGrid, true);
                UpdateGrid(enemyGrid, false);
            });
        }

        private void CreateGrid(Grid grid, bool isPlayerGrid)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                grid.Children.Clear();
                grid.RowDefinitions.Clear();
                grid.ColumnDefinitions.Clear();

                for (int i = 0; i < 10; i++)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                for (int x = 0; x < 10; x++)
                {
                    for (int y = 0; y < 10; y++)
                    {
                        var button = new Button
                        {
                            BackgroundColor = Colors.LightBlue,
                            BorderColor = Colors.Black,
                            BorderWidth = 1,
                            CornerRadius = 0
                        };

                        if (isPlayerGrid)
                        {
                            int currentX = x;
                            int currentY = y;
                            button.Clicked += (s, e) => onPlayerGridClicked(currentX, currentY);
                        }
                        else
                        {
                            int currentX = x;
                            int currentY = y;
                            button.Clicked += (s, e) => onEnemyGridClicked(currentX, currentY);
                        }

                        grid.Children.Add(button);
                        Grid.SetRow(button, y);
                        Grid.SetColumn(button, x);
                    }
                }

                UpdateBoardDisplay();
            });
        }

        private void UpdateGrid(Grid grid, bool isPlayerGrid)
        {
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    var button = grid.Children
                        .OfType<Button>()
                        .FirstOrDefault(b => Grid.GetRow(b) == y && Grid.GetColumn(b) == x);

                    if (button != null)
                    {
                        var cell = isPlayerGrid ?
                            gameEngine.PlayerBoard[x, y] :
                            gameEngine.EnemyBoard[x, y];

                        button.BackgroundColor = cell switch
                        {
                            CellState.Ship => Colors.Gray,
                            CellState.Hit => Colors.Red,
                            CellState.Miss => Colors.White,
                            _ => Colors.LightBlue
                        };
                    }
                }
            }
        }
    }
}