using Microsoft.AspNetCore.SignalR.Client;
using System;

namespace PerudoGame.Client.Services
{
    public class GameClientLogic
    {

        private readonly HubConnection _hubConnection;

        public event Action<List<string>>? OnPlayerListUpdated;
        public event Action<string, string>? OnChatUpdated;
        public event Action<string>? ErrorHandle;
        public event Func<Task> OnEndGame;
        public event Action<int> OnYourTurn;
        private string _gameName;
        public event Func<List<string>, string,Task> OnTurnOrderDetermined;
        public event Func<string, string, Task> OnTurnChanged;
        public event Func<string, Task> OnInvalidTurn;
        public event Func<Task> OnReadyConfirmed;
        public event Func<Task> OnMaputaRound;
        public event Func<List<int>, Task> OnDiceUpdated;
        public event Func<List<int>, Task> OnBetUpdated;
        public event Func<Dictionary<string, int>, Task> OnDicesCountUpdated;

        public GameClientLogic(string hubUrl)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<string>("Error", (error) =>
            {
                ErrorHandle?.Invoke(error);
            });

            _hubConnection.On<string, string>("ChatUpdated", (name, action) =>
            {
                OnChatUpdated?.Invoke(name, action);
            });

            _hubConnection.On<List<string>>("PlayerListUpdate", (playerNames) =>
            {
                OnPlayerListUpdated?.Invoke(playerNames);
            });

            _hubConnection.On<List<string>>("PlayersListAskAsync", (playerNames) =>
            {
                OnPlayerListUpdated?.Invoke(playerNames);
            });

            _hubConnection.Reconnected += async (connectionId) =>
            {
                Console.WriteLine($"Reconnected with connectionId: {connectionId}");
            };

            _hubConnection.Closed += async (error) =>
            {
                Console.WriteLine($"Connection closed. Error: {error?.Message}");
                await Task.Delay(5000);
                await _hubConnection.StartAsync();
            };

            _hubConnection.On<List<string>, string>("TurnOrderDetermined", async (players, currentPlayer) =>
            {
                if (OnTurnOrderDetermined != null)
                    await OnTurnOrderDetermined(players, currentPlayer);
            });

            _hubConnection.On<string, string>("TurnChanged", async (currentPlayer, nextPlayer) =>
            {
                if (OnTurnChanged != null)
                    await OnTurnChanged(currentPlayer, nextPlayer);
            });

            _hubConnection.On("ReadyConfirmed", async () =>
            {
                if (OnReadyConfirmed != null)
                    await OnReadyConfirmed();
            });

            _hubConnection.On("MaputaRound", async () =>
            {
                //Console.WriteLine("MaputaRoun");
                if (OnMaputaRound != null)
                    await OnMaputaRound();
            });

            _hubConnection.On<List<int>>("yourDice", async (dice) =>
            {
                Console.WriteLine($"YOUR DICES ======");
                foreach(var d in dice)
                {
                    Console.WriteLine(d);
                }
                if (OnDiceUpdated != null)
                    await OnDiceUpdated(dice);
            });

            _hubConnection.On("endGame", async () =>
            {
                if (OnEndGame != null)
                    await OnEndGame();
            });

            _hubConnection.On<Dictionary<string, int>>("DicesCount", async (dicesCount) =>
            {
                if (OnDicesCountUpdated != null)
                    await OnDicesCountUpdated(dicesCount);
            });

            _hubConnection.On<List<int>>("currentBet", async (bet) =>
            {
                if (OnBetUpdated != null)
                    await OnBetUpdated(bet);
            });
        }

        public async Task ConnectAsync() => await _hubConnection.StartAsync();
        public async Task JoinGame(string playerName, string gameName)
        {
            _gameName = gameName;
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
            }

            await _hubConnection.InvokeCoreAsync("JoinGame", args: new[] { gameName,  playerName  });
        }
        public async Task PlayersListAskAsync()
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
            }

            await _hubConnection.InvokeCoreAsync("PlayersListAskAsync", args: new[] { _gameName });// typeof(List<string>), []);
        }
        public async Task StartGame() => await _hubConnection.InvokeAsync("StartGame", _gameName);
        public async Task DetermineTurnOrder()
        {
            await _hubConnection.InvokeAsync("DetermineTurnOrder", _gameName);
        }
        public async Task MakeMove(string playerName, string moveType, List<int> args)
        {
            Console.WriteLine($"GameClientLogic playerName: {playerName}, moveType: {moveType}");
            await _hubConnection.InvokeAsync("MakeMove", _gameName, playerName, moveType, args);
        }
        public async Task SetReady()
        {
            await _hubConnection.InvokeAsync("SetReady", _gameName);
        }
        public async Task LeaveGame(string playerName)
        {
            await _hubConnection.InvokeAsync("LeaveGame", _gameName, playerName);
        }
        public async Task Maputa()
        {
            await _hubConnection.InvokeAsync("MaputaRound", _gameName);
        }

        public async Task SendMessageToChat(string playerName, string text)
        {
            await _hubConnection.InvokeAsync("SendMessageToChat", _gameName, playerName, text);
        }
        public string GameName
        {
            get { return _gameName; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("GameName cannot be null or empty.");
                }
                _gameName = value;
            }
        }

    }
}
