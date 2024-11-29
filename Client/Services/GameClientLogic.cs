using Microsoft.AspNetCore.SignalR.Client;

namespace PerudoGame.Client.Services;

public class GameClientLogic
{
    private readonly HubConnection _hubConnection;

    public event Action<List<string>>? OnPlayerListUpdated;
    public event Action OnPlayerListUpdated2;
    public event Action GameStarted;
    public event Action<int> YourTurn;

    public event Action<List<Player>>? OnGameStarted;
    public event Action<string, string>? OnBidMade;

    public GameClientLogic(string hubUrl)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect() // Автоматическое переподключение
            .Build();

        _hubConnection.On("GameStarted", () =>
        {
            Console.WriteLine("----Game Started----");
            GameStarted?.Invoke(); 
        });

        _hubConnection.On<int>("YourTurn", (newCounter) =>
        {
            YourTurn?.Invoke(newCounter);
        });

        _hubConnection.On<List<string>>("UpdatePlayerList", (playerNames) =>
        {
            OnPlayerListUpdated?.Invoke(playerNames); // Вызываем локальное событие
            OnPlayerListUpdated2?.Invoke();
        });

        _hubConnection.On<List<string>>("PlayersListAskAsync", (playerNames) =>
        {
            OnPlayerListUpdated?.Invoke(playerNames); // Вызываем локальное событие
            OnPlayerListUpdated2?.Invoke();
        });

        _hubConnection.Reconnected += async (connectionId) =>
        {
            Console.WriteLine($"Reconnected with connectionId: {connectionId}");
        };

        _hubConnection.Closed += async (error) =>
        {
            Console.WriteLine($"Connection closed. Error: {error?.Message}");
            await Task.Delay(5000); // Подождите перед переподключением
            await _hubConnection.StartAsync(); // Переподключение
        };
    }

    public async Task IncCounter() => await _hubConnection.InvokeCoreAsync("CounterIncremented", typeof(Task), [] );
    public async Task ConnectAsync() => await _hubConnection.StartAsync();
    public async Task JoinGame(string playerName)
    {
        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            await _hubConnection.StartAsync(); // Установить соединение
        }

        await _hubConnection.InvokeCoreAsync("JoinGame", args: new[] { playerName });
    }
    public async Task PlayersListAskAsync()
    {
      
        if (_hubConnection.State == HubConnectionState.Disconnected)
        {
            await _hubConnection.StartAsync(); // Установить соединение
        }

        await _hubConnection.InvokeCoreAsync("PlayersListAskAsync", typeof(List<string>), []);

    }
    public async Task StartGame() => await _hubConnection.InvokeAsync("StartGame");
    public async Task MakeBid(string playerName, string bid) => await _hubConnection.InvokeAsync("MakeBid", playerName, bid);
}

public class Player
{
    public string Name { get; set; }
    public List<int> Dice { get; set; } = new List<int>();
}
