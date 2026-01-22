using SeaBattle.Models;

namespace SeaBattle.Server
{
    /// <summary>
    /// Handles network messages for the Sea Battle game
    /// </summary>
    public class NetworkMessageHandler
    {
        private GameEngine gameEngine;
        private GameServer server;
        private Func<bool> getIsGameOver;
        private Action<string> updateGameStatus;
        private Action<bool> endGame;
        private Func<bool> checkEnemyWinCondition;
        private Func<string, int, int, Task> processEnemySpecialAttack;

        /// <summary>
        /// Initializes a new instance of the NetworkMessageHandler class
        /// </summary>
        public NetworkMessageHandler(GameEngine gameEngine, GameServer server,
                                   Func<bool> getIsGameOver, Action<string> updateGameStatus,
                                   Action<bool> endGame, Func<bool> checkEnemyWinCondition,
                                   Func<string, int, int, Task> processEnemySpecialAttack)
        {
            this.gameEngine = gameEngine;
            this.server = server;
            this.getIsGameOver = getIsGameOver;
            this.updateGameStatus = updateGameStatus;
            this.endGame = endGame;
            this.checkEnemyWinCondition = checkEnemyWinCondition;
            this.processEnemySpecialAttack = processEnemySpecialAttack;
        }

        /// <summary>
        /// Handles incoming messages from the network opponent
        /// </summary>
        public async void OnMessageReceived(string ip, string message)
        {
            if (getIsGameOver()) return;

            if (message.StartsWith("SHOT:"))
            {
                await HandleShotMessage(message);
            }
            else if (message.StartsWith("RESULT:"))
            {
                await HandleResultMessage(message);
            }
            else if (message == "READY")
            {
                HandleReadyMessage();
            }
            else if (message == "WIN")
            {
                await HandleWinMessage();
            }
            else if (message.StartsWith("SPECIAL:"))
            {
                await HandleSpecialAttackMessage(message);
            }
            else if (message.StartsWith("SPECIAL_RESULT:"))
            {
                await HandleSpecialResultMessage(message);
            }
        }

        private void HandleReadyMessage()
        {
            gameEngine.SetEnemyReady(true);
        }

        private async Task HandleWinMessage()
        {
            endGame(true);
        }

        private async Task HandleShotMessage(string message)
        {
            var parts = message.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
            {
                bool hit = gameEngine.ProcessEnemyShot(x, y);
                await server.SendMessage($"RESULT:{x}:{y}:{(hit ? "HIT" : "MISS")}");

                if (!hit)
                {
                    gameEngine.SetPlayerTurn(true);
                    updateGameStatus("Your turn - enemy missed!");
                }
                else
                {
                    updateGameStatus("Enemy hit your ship! Their turn continues...");

                    if (gameEngine.CheckWinCondition())
                    {
                        endGame(false);
                        await server.SendMessage("WIN");
                    }
                }
            }
        }

        private async Task HandleResultMessage(string message)
        {
            var parts = message.Split(':');
            if (parts.Length == 4 && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int y))
            {
                bool hit = parts[3] == "HIT";
                gameEngine.MarkShotResult(x, y, hit);


                if (!hit)
                {
                    gameEngine.SetPlayerTurn(false);
                    updateGameStatus("You missed! Enemy's turn...");
                }
                else
                {
                    updateGameStatus("You hit enemy ship! Your turn continues...");

                    if (checkEnemyWinCondition())
                    {
                        endGame(true);
                    }
                }
            }
        }

        private async Task HandleSpecialAttackMessage(string message)
        {
            var parts = message.Split(':');
            if (parts.Length >= 4)
            {
                string attackType = parts[1];
                int startX = int.Parse(parts[2]);
                int startY = int.Parse(parts[3]);

                await processEnemySpecialAttack(attackType, startX, startY);
            }
        }

        private async Task HandleSpecialResultMessage(string message)
        {
            var parts = message.Split(':');
            if (parts.Length >= 2)
            {
                bool hit = parts[1] == "HIT";

                if (!hit)
                {
                    gameEngine.SetPlayerTurn(false);
                    updateGameStatus("Your special attack missed! Enemy's turn!");
                }
                else
                {
                    updateGameStatus("Your special attack hit! Your turn continues...");
                    gameEngine.SetPlayerTurn(true);

                    if (checkEnemyWinCondition())
                    {
                        endGame(true);
                    }
                }
            }
        }
    }
}
