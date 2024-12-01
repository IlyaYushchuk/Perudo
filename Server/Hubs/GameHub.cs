using Microsoft.AspNetCore.SignalR;
using Microsoft.VisualBasic;
using PerudoGame.Server.Models;
using System.Diagnostics.Metrics;

namespace PerudoGame.Server.Hubs;

public class GameHub : Hub
{
    private readonly GameLogic _gameLogic;
    public GameHub(GameLogic gameLogic)
    {
        _gameLogic = gameLogic;
    }
    public async Task JoinGame(string playerName)
    {
        _gameLogic.AddPlayer(playerName, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "GameGroup");
      
        await Clients.All.SendAsync("PlayerListUpdate", _gameLogic.Players.Select(p => p.Name).ToList());
    }
    public async Task SetReady()
    {
        bool allReady = _gameLogic.SetPlayerReady(Context.ConnectionId);
        await Clients.Caller.SendAsync("ReadyConfirmed");

        if (allReady)
        {
            List<List<string>> dices = await _gameLogic.DetermineTurnOrder();
            foreach (var item in dices)
            {
                await Clients.All.SendAsync("ChatUpdated", item[0], $"У меня выпало {item[1]}");
            }
            var currentPlayer = _gameLogic.GetCurrentPlayer();
            await Clients.All.SendAsync("TurnOrderDetermined", _gameLogic.Players.Select(p => p.Name).ToList(), currentPlayer);
        }
    }
    public async Task PlayersListAskAsync()
    {
        await Clients.Caller.SendAsync("PlayersListAskAsync", _gameLogic.Players.Select(p => p.Name).ToList());
    }
    public async Task MakeMove(string playerName, string moveType, List<int> args)
    {
        var currentPlayer = _gameLogic.GetCurrentPlayer();

        if (moveType == "makeBet")
        {
            var bet = _gameLogic.MakeBet(playerName, args[0], args[1]);
            _gameLogic.NextTurn();
            Console.WriteLine($"---{bet.Value.Number} {bet.Value.Count}");
            await Clients.All.SendAsync("currentBet", new List<int> { bet.Value.Number, bet.Value.Count });
            await Clients.All.SendAsync("ChatUpdated", playerName, $"Думаю, что на столе есть {bet.Value.Count} костей со значением {bet.Value.Number}");
        }
        else if (moveType == "startRound")
        {
            Dictionary<string, List<int>> diceInfo = _gameLogic.StartRound();
            foreach (var d in diceInfo)
            {
                await Clients.Client(d.Key).SendAsync("yourDice", d.Value);
            }
        }
        else if (moveType == "doodoo")
        {
            await Clients.All.SendAsync("ChatUpdated", playerName, "Не верю, объявляю Doodoo!");
            (Dictionary<string, int> diceCount, string loser, bool lose) = _gameLogic.CallDoodoo(playerName);
            await Clients.All.SendAsync("DicesCount", diceCount);
            await Clients.All.SendAsync("ChatUpdated", loser, $"Я потерял свою кость. Теперь у меня осталось {diceCount[loser]}");
            if (lose)
            {
                await Clients.All.SendAsync("ChatUpdated", loser, $"Я проиграл");
            }
            if (!_gameLogic.GameStarted)
            {
                _gameLogic._readyPlayers = new();
                foreach (var d in diceCount)
                {
                    if (d.Value > 0)
                        await Clients.All.SendAsync("ChatUpdated", loser, $"Игра окончена! Победил игрок {d.Key}");
                }
                foreach (var pl in _gameLogic.Players)
                {
                    pl.DiceCount = 1;
                }
                await Clients.All.SendAsync("endGame");
            }
        }

        currentPlayer = _gameLogic.GetCurrentPlayer();
        await Clients.All.SendAsync("TurnChanged", currentPlayer);
    }
}