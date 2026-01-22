namespace SeaBattle.Models
{
    public class GameEngine
    {
        public CellState[,] PlayerBoard { get; private set; }
        public CellState[,] EnemyBoard { get; private set; }
        public GameState CurrentState { get; private set; }
        public bool IsPlayerTurn { get; private set; }
        public bool IsPlayerReady { get; private set; }
        public bool IsEnemyReady { get; private set; }
        public int PlacedShipsCount => ships.Count(s => s.IsPlaced);
        public int TotalShipsCount => ships.Length;
        public int CurrentShipSize => currentShipIndex < ships.Length ? ships[currentShipIndex].Size : 0;
        public bool IsShipHorizontal => isShipHorizontal;
        public bool HasLineHorizontalAttack { get; private set; } = true;
        public bool HasLineVerticalAttack { get; private set; } = true;
        public AttackType SelectedSpecialAttack { get; set; } = AttackType.Standard;

        private Ship[] ships;
        private int currentShipIndex = 0;
        private bool isShipHorizontal = false;

        private readonly Dictionary<int, int> shipConfiguration = new Dictionary<int, int>
{
    { 4, 1 },
    { 3, 2 },
    { 2, 3 },
    { 1, 4 }
};

        public event Action<GameState> GameStateChanged;
        public event Action BoardUpdated;
        public event Action SpecialAttacksUpdated;

        /// <summary>
        /// Initializes a new instance of the GameEngine class
        /// </summary>
        public GameEngine()
        {
            ResetGame();
        }

        /// <summary>
        /// Resets the game to its initial state
        /// </summary>
        public void ResetGame()
        {
            PlayerBoard = new CellState[10, 10];
            EnemyBoard = new CellState[10, 10];

            CurrentState = GameState.ShipPlacement;
            IsPlayerTurn = false;
            IsPlayerReady = false;
            IsEnemyReady = false;
            currentShipIndex = 0;
            isShipHorizontal = false;

            HasLineHorizontalAttack = true;
            HasLineVerticalAttack = true;
            SelectedSpecialAttack = AttackType.Standard;

            InitializeShips();

            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
        }

        /// <summary>
        /// Starts the game after both players are ready
        /// </summary>
        public void StartGame()
        {
            CurrentState = GameState.Player1Turn;
            IsPlayerTurn = true;
            GameStateChanged?.Invoke(CurrentState);
        }

        /// <summary>
        /// Checks if all ships have been placed
        /// </summary>
        public bool AllShipsPlaced()
        {
            return ships.All(s => s.IsPlaced);
        }

        /// <summary>
        /// Places the current ship at the specified coordinates
        /// </summary>
        public bool PlaceShip(int x, int y)
        {
            if (currentShipIndex >= ships.Length)
                return false;

            var ship = ships[currentShipIndex];

            if (!CanPlaceShip(x, y, ship.Size, isShipHorizontal))
                return false;

            for (int i = 0; i < ship.Size; i++)
            {
                int posX = isShipHorizontal ? x + i : x;
                int posY = isShipHorizontal ? y : y + i;

                if (posX < 10 && posY < 10)
                {
                    PlayerBoard[posX, posY] = CellState.Ship;
                }
            }

            ship.X = x;
            ship.Y = y;
            ship.IsHorizontal = isShipHorizontal;
            ship.IsPlaced = true;
            currentShipIndex++;

            BoardUpdated?.Invoke();
            return true;
        }

        /// <summary>
        /// Rotates the current ship orientation
        /// </summary>
        public void RotateShip()
        {
            isShipHorizontal = !isShipHorizontal;
        }

        /// <summary>
        /// Sets the player's ready status
        /// </summary>
        public void SetPlayerReady(bool ready)
        {
            IsPlayerReady = ready;
        }

        /// <summary>
        /// Sets the enemy's ready status
        /// </summary>
        public void SetEnemyReady(bool ready)
        {
            IsEnemyReady = ready;

            if (IsPlayerReady && IsEnemyReady)
            {
                StartGame();
            }
        }


        /// <summary>
        /// Sets whose turn it is
        /// </summary>
        public void SetPlayerTurn(bool playerTurn)
        {
            IsPlayerTurn = playerTurn;
            CurrentState = playerTurn ? GameState.Player1Turn : GameState.Player2Turn;
            GameStateChanged?.Invoke(CurrentState);
        }

        /// <summary>
        /// Executes an attack at the specified coordinates
        /// </summary>
        public (int x, int y, bool hit)[] ExecuteAttack(int x, int y)
        {
            switch (SelectedSpecialAttack)
            {
                case AttackType.LineHorizontal:
                    return ExecuteLineHorizontalAttack(x, y);

                case AttackType.LineVertical:
                    return ExecuteLineVerticalAttack(x, y);

                case AttackType.Standard:
                default:
                    var shot = new List<(int, int, bool)>();

                    if (EnemyBoard[x, y] == CellState.Hit || EnemyBoard[x, y] == CellState.Miss)
                        return shot.ToArray();

                    if (EnemyBoard[x, y] == CellState.Empty)
                    {
                        EnemyBoard[x, y] = CellState.Miss;
                        shot.Add((x, y, false));
                    }
                    else if (EnemyBoard[x, y] == CellState.Ship)
                    {
                        EnemyBoard[x, y] = CellState.Hit;
                        shot.Add((x, y, true));
                    }

                    BoardUpdated?.Invoke();
                    return shot.ToArray();
            }
        }

        /// <summary>
        /// Selects a special attack type
        /// </summary>
        public void SelectSpecialAttack(AttackType attack)
        {
            if (CurrentState != GameState.Player1Turn || !IsPlayerTurn)
                return;

            SelectedSpecialAttack = attack;
            SpecialAttacksUpdated?.Invoke();
        }

        /// <summary>
        /// Processes an enemy shot at the specified coordinates
        /// </summary>
        public bool ProcessEnemyShot(int x, int y)
        {
            if (PlayerBoard[x, y] == CellState.Hit || PlayerBoard[x, y] == CellState.Miss)
                return false;

            if (PlayerBoard[x, y] == CellState.Ship)
            {
                PlayerBoard[x, y] = CellState.Hit;
                BoardUpdated?.Invoke();
                return true;
            }
            else if (PlayerBoard[x, y] == CellState.Empty)
            {
                PlayerBoard[x, y] = CellState.Miss;
                BoardUpdated?.Invoke();
                return false;
            }
            return false;
        }

        /// <summary>
        /// Marks the result of a shot on the enemy board
        /// </summary>
        public void MarkShotResult(int x, int y, bool hit)
        {
            if (EnemyBoard[x, y] == CellState.Hit || EnemyBoard[x, y] == CellState.Miss)
                return;

            EnemyBoard[x, y] = hit ? CellState.Hit : CellState.Miss;
            BoardUpdated?.Invoke();
        }

        /// <summary>
        /// Checks if the player has won (all enemy ships destroyed)
        /// </summary>
        public bool CheckWinCondition()
        {
            return !PlayerBoard.Cast<CellState>().Any(cell => cell == CellState.Ship);
        }

        private void InitializeShips()
        {
            var shipList = new List<Ship>();

            foreach (var config in shipConfiguration)
            {
                int size = config.Key;
                int count = config.Value;

                for (int i = 0; i < count; i++)
                {
                    shipList.Add(new Ship { Size = size });
                }
            }

            ships = shipList.ToArray();
        }

        private bool CanPlaceShip(int x, int y, int shipSize, bool isHorizontal)
        {
            if (isHorizontal)
            {
                if (x + shipSize > 10) return false;
            }
            else
            {
                if (y + shipSize > 10) return false;
            }

            for (int i = 0; i < shipSize; i++)
            {
                int checkX = isHorizontal ? x + i : x;
                int checkY = isHorizontal ? y : y + i;

                if (checkX >= 10 || checkY >= 10)
                    return false;

                if (PlayerBoard[checkX, checkY] != CellState.Empty)
                    return false;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int aroundX = checkX + dx;
                        int aroundY = checkY + dy;

                        if (aroundX >= 0 && aroundX < 10 && aroundY >= 0 && aroundY < 10)
                        {
                            if (PlayerBoard[aroundX, aroundY] == CellState.Ship)
                                return false;
                        }
                    }
                }
            }

            return true;
        }

        public (int x, int y, bool hit)[] ExecuteLineHorizontalAttack(int startX, int startY)
        {
            if (!HasLineHorizontalAttack)
                return Array.Empty<(int, int, bool)>();

            var shots = new List<(int x, int y, bool hit)>();

            for (int x = 0; x < 10; x++)
            {
                if (EnemyBoard[x, startY] == CellState.Hit || EnemyBoard[x, startY] == CellState.Miss)
                    continue;

                if (EnemyBoard[x, startY] == CellState.Ship)
                {
                    EnemyBoard[x, startY] = CellState.Hit;
                    shots.Add((x, startY, true));
                }
                else if (EnemyBoard[x, startY] == CellState.Empty)
                {
                    EnemyBoard[x, startY] = CellState.Miss;
                    shots.Add((x, startY, false));
                }
            }

            HasLineHorizontalAttack = false;
            SelectedSpecialAttack = AttackType.Standard;
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
            return shots.ToArray();
        }

        public (int x, int y, bool hit)[] ExecuteLineVerticalAttack(int startX, int startY)
        {
            if (!HasLineVerticalAttack)
                return Array.Empty<(int, int, bool)>();

            var shots = new List<(int x, int y, bool hit)>();

            for (int y = 0; y < 10; y++)
            {
                if (EnemyBoard[startX, y] == CellState.Hit || EnemyBoard[startX, y] == CellState.Miss)
                    continue;

                if (EnemyBoard[startX, y] == CellState.Ship)
                {
                    EnemyBoard[startX, y] = CellState.Hit;
                    shots.Add((startX, y, true));
                }
                else if (EnemyBoard[startX, y] == CellState.Empty)
                {
                    EnemyBoard[startX, y] = CellState.Miss;
                    shots.Add((startX, y, false));
                }
            }

            HasLineVerticalAttack = false;
            SelectedSpecialAttack = AttackType.Standard;
            BoardUpdated?.Invoke();
            SpecialAttacksUpdated?.Invoke();
            return shots.ToArray();
        }
    }
}
