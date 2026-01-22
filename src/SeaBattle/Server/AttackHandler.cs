using SeaBattle.Models;

namespace SeaBattle.Server
{
    /// <summary>
    /// Handles special attacks for the Sea Battle game
    /// </summary>
    public class AttackHandler
    {
        private GameEngine gameEngine;
        private GameServer server;
        private Action updateBoardDisplay;
        private Action updateSpecialAttacks;
        private Action<bool> endGame;
        private Func<bool> checkEnemyWinCondition;
        private Action<string> updateGameStatus;

        /// <summary>
        /// Initializes a new instance of the AttackHandler class
        /// </summary>
        public AttackHandler(GameEngine gameEngine, GameServer server,
                                  Action updateBoardDisplay, Action updateSpecialAttacks,
                                  Action<bool> endGame, Func<bool> checkEnemyWinCondition,
                                  Action<string> updateGameStatus)
        {
            this.gameEngine = gameEngine;
            this.server = server;
            this.updateBoardDisplay = updateBoardDisplay;
            this.updateSpecialAttacks = updateSpecialAttacks;
            this.endGame = endGame;
            this.checkEnemyWinCondition = checkEnemyWinCondition;
            this.updateGameStatus = updateGameStatus;
        }

        /// <summary>
        /// Executes a special attack on the enemy
        /// </summary>
        public async Task ExecuteSpecialAttack(int x, int y)
        {
            (int x, int y, bool hit)[] shots = Array.Empty<(int, int, bool)>();
            string attackName = "";
            string attackType = "";

            switch (gameEngine.SelectedSpecialAttack)
            {
                case AttackType.LineHorizontal:
                    shots = gameEngine.ExecuteLineHorizontalAttack(x, y);
                    attackName = "Horizontal Line";
                    attackType = "HorizontalLine";
                    break;
                case AttackType.LineVertical:
                    shots = gameEngine.ExecuteLineVerticalAttack(x, y);
                    attackName = "Vertical Line";
                    attackType = "VerticalLine";
                    break;
            }

            await server.SendMessage($"SPECIAL:{attackType}:{x}:{y}");
            updateBoardDisplay();
            updateSpecialAttacks();

            bool hitSomething = shots.Any(shot => shot.hit);
            int hitCount = shots.Count(shot => shot.hit);

            if (checkEnemyWinCondition())
            {
                endGame(true);
                return;
            }

            if (hitCount > 0)
            {
                updateGameStatus($"{attackName} attack hit {hitCount} time(s)! Your turn continues...");
                gameEngine.SetPlayerTurn(true);
            }
            else
            {
                updateGameStatus($"{attackName} attack missed! Enemy's turn...");
                gameEngine.SetPlayerTurn(false);
            }
        }

        /// <summary>
        /// Processes a special attack from the enemy
        /// </summary>
        public async Task ProcessEnemySpecialAttack(string attackType, int startX, int startY)
        {
            string attackName = attackType switch
            {
                "HorizontalLine" => "Horizontal Line",
                "VerticalLine" => "Vertical Line",
                _ => "Special Attack"
            };

            bool hitSomething = false;

            switch (attackType)
            {
                case "HorizontalLine":
                    hitSomething = ProcessHorizontalLineAttackOnPlayer(startX, startY);
                    break;
                case "VerticalLine":
                    hitSomething = ProcessVerticalLineAttackOnPlayer(startX, startY);
                    break;
            }

            await server.SendMessage($"SPECIAL_RESULT:{(hitSomething ? "HIT" : "MISS")}");
            updateBoardDisplay();


            if (gameEngine.CheckWinCondition())
            {
                endGame(false);
                await server.SendMessage("WIN");
                return;
            }

            if (!hitSomething)
            {
                gameEngine.SetPlayerTurn(true);
                updateGameStatus($"Enemy used {attackName} attack and missed! Your turn!");
            }
            else
            {
                updateGameStatus($"Enemy used {attackName} attack and hit! Their turn continues...");
            }
        }

        private bool ProcessHorizontalLineAttackOnPlayer(int startX, int startY)
        {
            bool hitSomething = false;

            for (int x = 0; x < 10; x++)
            {
                if (gameEngine.PlayerBoard[x, startY] == CellState.Hit || gameEngine.PlayerBoard[x, startY] == CellState.Miss)
                    continue;

                if (gameEngine.PlayerBoard[x, startY] == CellState.Ship)
                {
                    gameEngine.PlayerBoard[x, startY] = CellState.Hit;
                    hitSomething = true;
                }
                else if (gameEngine.PlayerBoard[x, startY] == CellState.Empty)
                {
                    gameEngine.PlayerBoard[x, startY] = CellState.Miss;
                }
            }

            return hitSomething;
        }

        private bool ProcessVerticalLineAttackOnPlayer(int startX, int startY)
        {
            bool hitSomething = false;

            for (int y = 0; y < 10; y++)
            {
                if (gameEngine.PlayerBoard[startX, y] == CellState.Hit || gameEngine.PlayerBoard[startX, y] == CellState.Miss)
                    continue;

                if (gameEngine.PlayerBoard[startX, y] == CellState.Ship)
                {
                    gameEngine.PlayerBoard[startX, y] = CellState.Hit;
                    hitSomething = true;
                }
                else if (gameEngine.PlayerBoard[startX, y] == CellState.Empty)
                {
                    gameEngine.PlayerBoard[startX, y] = CellState.Miss;
                }
            }

            return hitSomething;
        }
    }
}
