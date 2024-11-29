using Microsoft.AspNetCore.SignalR;
using Microsoft.VisualBasic;
using PerudoGame.Server.Models;
using System.Diagnostics.Metrics;

namespace PerudoGame.Server.Hubs;

public class GameHub : Hub
{
    private readonly GameLogic _gameLogic;
    private static List<string> _connectionIdToIndex = new();
    private static int counter { get; set; } = 1;
    public GameHub(GameLogic gameLogic)
    {
        _gameLogic = gameLogic;
        
    }

    public async Task JoinGame(string playerName)
    {
        _gameLogic.AddPlayer(playerName);
        await Groups.AddToGroupAsync(Context.ConnectionId, "GameGroup");
        Console.WriteLine($"ConnectionID: {Context.ConnectionId} name: {playerName}");
        
        Console.WriteLine($"counter: {counter}");
        _connectionIdToIndex.Add(Context.ConnectionId);
        Console.WriteLine($"_connectionIdToIndex: {_connectionIdToIndex.Count} ");

        // await Clients.All.UpdatePlayerList(_gameLogic.Players.Select(p => p.Name).ToList());
        var playersNames = _gameLogic.Players.Select(p => p.Name).ToList();
      
        await Clients.All.SendAsync("UpdatePlayerList", playersNames);
    }
    public async Task PlayersListAskAsync()
    {
        var playersNames = _gameLogic.Players.Select(p => p.Name).ToList();
        await Clients.Caller.SendAsync("PlayersListAskAsync", playersNames);
    }
    public async Task CounterIncremented()
    {
        counter++;
        Console.WriteLine($"{counter} incremented");
        var connectionId = Context.ConnectionId;
        int nextIndex = (_connectionIdToIndex.IndexOf(connectionId) + 1) % _connectionIdToIndex.Count;

        await Clients.Client(_connectionIdToIndex[nextIndex]).SendAsync("YourTurn", counter);
        Console.WriteLine($"{_connectionIdToIndex[nextIndex]}");
    }
    public async Task StartGame()
    {
        if (_gameLogic.StartGame())
        {
            await Clients.All.SendAsync("GameStarted");
            int firstPlayerIndex = _gameLogic.FirstPlayerChoice();

            
            string connectionId = _connectionIdToIndex[firstPlayerIndex];
            await Clients.Client(connectionId).SendAsync("YourTurn", counter);
            Console.WriteLine($"{connectionId}");
        }
    }

    public async Task MakeBid(string playerName, string bid)
    {
        _gameLogic.MakeBid(playerName, bid);
        //await Clients.Group("GameGroup").BidMade(playerName, bid);
    }
}

public interface IGameClient
{
    Task UpdatePlayerList(List<string> playerNames);
    Task GameStarted(List<Player> players);
    Task BidMade(string playerName, string bid);
}
