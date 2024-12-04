using PerudoGame.Server.Models;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace PerudoGame.Server.Hubs;

public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, GameLogic> _games = new ConcurrentDictionary<string, GameLogic>();

    public async Task JoinGame(string gameName, string playerName)
    {
        if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(playerName))
        {
            await Clients.Caller.SendAsync("Error", "Game name and player name cannot be empty.");
            return;
        }

        var game = _games.GetOrAdd(gameName, _ => new GameLogic());
        try
        {
            game.AddPlayer(playerName, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"GameGroup_{gameName}");
            await Clients.Group($"GameGroup_{gameName}").SendAsync("PlayerListUpdate", game.Players.Select(p => p.Name).ToList());
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    public async Task SetReady(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            await Clients.Caller.SendAsync("Error", "Game name cannot be empty.");
            return;
        }

        if (!_games.TryGetValue(gameName, out var game))
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        try
        {
            bool allReady = game.SetPlayerReady(Context.ConnectionId);
            await Clients.Caller.SendAsync("ReadyConfirmed");

            if (allReady)
            {
                List<List<string>> dices = await game.DetermineTurnOrder();
                foreach (var item in dices)
                {
                    await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", item[0], $"У меня выпало {item[1]}");
                }
                var players = game.GetCurrentPlayer();
                var currentPlayer = players.Item1;
                var playersOrder = game.GetPlayersOrder();
                await Clients.Group($"GameGroup_{gameName}").SendAsync("TurnOrderDetermined", playersOrder, currentPlayer);
            }
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Group($"GameGroup_{gameName}").SendAsync("Error", ex.Message);
        }
    }

    public async Task LeaveGame(string gameName, string playerName)
    {
        if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(playerName))
        {
            await Clients.Caller.SendAsync("Error", "Game name and player name cannot be empty.");
            return;
        }

        if (!_games.TryGetValue(gameName, out var game))
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        try
        {
            game.LeavePlayerFromGame(Context.ConnectionId, playerName);
            await Clients.Group($"GameGroup_{gameName}").SendAsync("PlayerListUpdate", game.Players.Select(p => p.Name).ToList());
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
    }

    public async Task PlayersListAskAsync(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            await Clients.Caller.SendAsync("Error", "Game name cannot be empty.");
            return;
        }

        if (!_games.TryGetValue(gameName, out var game))
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        await Clients.Caller.SendAsync("PlayersListAskAsync", game.Players.Select(p => p.Name).ToList());
    }

    public async Task MaputaRound(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            await Clients.Caller.SendAsync("Error", "Game name cannot be empty.");
            return;
        }

        if (!_games.TryGetValue(gameName, out var game))
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        game.Maputa = true;
        await Clients.Group($"GameGroup_{gameName}").SendAsync("MaputaRound");
    }


    public async Task MakeMove(string gameName, string playerName, string moveType, List<int> args)
    {
        if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(moveType))
        {
            await Clients.Caller.SendAsync("Error", "Game name, player name, and move type cannot be empty.");
            return;
        }

        if (!_games.TryGetValue(gameName, out var game))
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        try
        {
            await HandleMove(game, gameName, playerName, moveType, args);
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Group($"GameGroup_{gameName}").SendAsync("Error", ex.Message);
        }
    }


    private async Task HandleMove(GameLogic game, string gameName, string playerName, string moveType, List<int> args)
    {

        var currentPlayer = game.GetCurrentPlayer().Item1;

        if (moveType == "makeBet")
        {
            var bet = game.MakeBet(playerName, args[0], args[1]);
            game.NextTurn();
            await Clients.Group($"GameGroup_{gameName}").SendAsync("currentBet", new List<int> { bet.Value.Number, bet.Value.Count });
            await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", playerName, $"Думаю, что на столе есть {bet.Value.Count} костей со значением {bet.Value.Number}");
        }
        else if (moveType == "startRound")
        {
            Dictionary<string, List<int>> diceInfo = game.StartRound();
            foreach (var d in diceInfo)
            {
                await Clients.Client(d.Key).SendAsync("yourDice", d.Value);
            }
        }
        else if (moveType == "doodoo")
        {
            await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", playerName, "Не верю, объявляю Doodoo!");
            (Dictionary<string, int> diceCount, string loser, bool lose) = game.CallDoodoo(playerName);
            await Clients.Group($"GameGroup_{gameName}").SendAsync("DicesCount", diceCount);
            await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", loser, $"Я потерял свою кость. Теперь у меня осталось {diceCount[loser]}");
            if (lose)
            {
                await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", loser, $"Я проиграл");
            }
            if (!game.GameStarted)
            {
                game._readyPlayers = new();
                foreach (var d in diceCount)
                {
                    if (d.Value > 0)
                        await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", loser, $"Игра окончена! Победил игрок {d.Key}");
                }
                foreach (var pl in game.Players)
                {
                    pl.DiceCount = 1;
                }
                await Clients.Group($"GameGroup_{gameName}").SendAsync("endGame");
            }
        }
        else if (moveType == "jonti")
        {

            Console.WriteLine($"GameHub playerName: {playerName}, moveType: {moveType}");

            await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", playerName, "Я объявляю Jonti!!!");

            (Dictionary<string, int> diceCount, string loser, bool lose) = game.CallJonti(playerName);

            Console.WriteLine($"GameHub loser: {loser}, lose: {lose}");
            foreach (var d in diceCount)
            {
                Console.WriteLine($"GameHub key: {d.Key}, value: {d.Value}");
            }


            await Clients.Group($"GameGroup_{gameName}").SendAsync("DicesCount", diceCount);
            if (lose)
            {
                await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", loser, $"Я потерял свою кость. Теперь у меня осталось {diceCount[loser]}");
            }
            else
            {
                await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", loser, $"Я приобрел новую кость. Теперь у меня {diceCount[loser]}");
            }
            if (diceCount[loser] == 0)
            {
                await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", loser, $"Я проиграл");
            }
            if (!game.GameStarted)
            {
                game._readyPlayers = new();
                foreach (var d in diceCount)
                {
                    if (d.Value > 0)
                        await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", loser, $"Игра окончена! Победил игрок {d.Key}");
                }
                foreach (var pl in game.Players)
                {
                    pl.DiceCount = 1;
                }
                await Clients.Group($"GameGroup_{gameName}").SendAsync("endGame");
            }
        }

        currentPlayer = game.GetCurrentPlayer().Item1;
        var nextPlayer = game.GetCurrentPlayer().Item2;
        await Clients.Group($"GameGroup_{gameName}").SendAsync("TurnChanged", currentPlayer, nextPlayer);

    }

    public async Task SendMessageToChat(string gameName, string playerName, string text)
    {
        if (string.IsNullOrWhiteSpace(gameName) || string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(text))
        {
            await Clients.Caller.SendAsync("Error", "Game name, player name, and message cannot be empty.");
            return;
        }

        if (!_games.TryGetValue(gameName, out var game))
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        await Clients.Group($"GameGroup_{gameName}").SendAsync("ChatUpdated", playerName, text);
    }
}

