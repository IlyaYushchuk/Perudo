using System;
using System.Collections.Generic;
using System.Linq;

namespace PerudoGame.Server.Models
{
    public class GameLogic
    {
        public List<Player> Players { get; set; } = new();
        public bool GameStarted { get; private set; } = false;

        private Queue<int> _turnOrder = new();
        private Dictionary<string, int> _connectionIdToPlayerIndex = new();
        public HashSet<int> _readyPlayers = new();
        public (int Number, int Count)? CurrentBet { get; private set; } = null;
        public Dictionary<string, List<int>> DiceRolls { get; private set; } = new();

        public void AddPlayer(string name, string connectionId)
        {
            if (GameStarted) throw new InvalidOperationException("Игра уже началась.");

            Players.Add(new Player(name));
            _connectionIdToPlayerIndex[connectionId] = Players.Count - 1;
        }
        public bool SetPlayerReady(string connectionId)
        {
            if (!_connectionIdToPlayerIndex.TryGetValue(connectionId, out var playerIndex))
                throw new InvalidOperationException("Player not found.");

            _readyPlayers.Add(playerIndex);
            return _readyPlayers.Count == Players.Count;
        }
        public async Task<List<List<string>>> DetermineTurnOrder()
        {
            GameStarted = true;

            var random = new Random();

            var rolls = Players.Select(player => new PlayerRoll
            {
                Player = player,
                Roll = random.Next(1, 7),
                RollsHistory = new List<int> { }
            }).ToList();

            foreach (var roll in rolls)
            {
                roll.RollsHistory.Add(roll.Roll);
            }

            var groupedRolls = rolls
                .GroupBy(r => r.Roll)
                .OrderByDescending(g => g.Key)
                .ToList();

            var turnOrder = new List<PlayerRoll>();

            foreach (var group in groupedRolls)
            {
                if (group.Count() == 1)
                {
                    turnOrder.Add(group.First());
                }
                else
                {
                    foreach (var playerRoll in group)
                    {
                        var newRoll = random.Next(1, 7);
                        playerRoll.RollsHistory.Add(newRoll);
                        playerRoll.Roll = newRoll;
                    }

                    var rerolledGroup = group.OrderByDescending(r => r.Roll).ToList();
                    turnOrder.AddRange(rerolledGroup);
                }
            }
            List<List<string>> dices = new();
            int i = 0;
            foreach (var playerRoll in rolls)
            {
                dices.Add(new());
                dices[i].Add(playerRoll.Player.Name);
                string allRolls = "";
                foreach (var roll in playerRoll.RollsHistory)
                {
                    allRolls += roll;
                    if (playerRoll.RollsHistory.IndexOf(roll) != playerRoll.RollsHistory.Count - 1)
                    {
                        allRolls += " -> ";
                    }
                }
                dices[i].Add(allRolls);
                i++;
            }

            var orderedPlayers = rolls.OrderByDescending(r => r.Roll).ToList();
            _turnOrder = new Queue<int>(orderedPlayers.Select(r => Players.IndexOf(r.Player)));

            return dices;
        }
        public string GetCurrentPlayer()
        {
            return _turnOrder.Count > 0 ? Players[_turnOrder.Peek()].Name : null;
        }
        public void NextTurn()
        {
            if (_turnOrder.Count > 0)
            {
                int currentPlayer = _turnOrder.Dequeue();
                _turnOrder.Enqueue(currentPlayer);
            }
        }
        public Dictionary<string, List<int>> StartRound()
        {
            var random = new Random();
            CurrentBet = null;
            DiceRolls.Clear();
            Dictionary<string, List<int>> diceInfo = new();

            foreach (var player in Players)
            {
                List<int> list = Enumerable.Range(0, player.DiceCount).Select(_ => random.Next(1, 7)).ToList();
                DiceRolls[player.Name] = list;

                var playerIndex = Players.FindIndex(player2 => player2.Name == player.Name);
                diceInfo[_connectionIdToPlayerIndex.FirstOrDefault(pair => pair.Value == playerIndex).Key] = list;
            }
            return diceInfo;
        }

        public (int Number, int Count)? MakeBet(string playerName, int number, int count)
        {
            if (!GameStarted)
                throw new InvalidOperationException("Игра не началась.");

            if (Players[_turnOrder.Peek()].Name != playerName)
                throw new InvalidOperationException("Сейчас не ваш ход.");

            if (CurrentBet != null)
            {
                if (count < CurrentBet.Value.Count || (count == CurrentBet.Value.Count && number <= CurrentBet.Value.Number))
                    throw new InvalidOperationException("Ставка должна быть выше текущей.");
            }

            CurrentBet = (number, count);
            //Console.WriteLine($"---{CurrentBet.Value.Number} {CurrentBet.Value.Count}");
            return CurrentBet;
        }

        public (Dictionary<string, int>, string, bool) CallDoodoo(string playerName)
        {
            if (!GameStarted)
                throw new InvalidOperationException("Игра не началась.");

            if (Players[_turnOrder.Peek()].Name != playerName)
                throw new InvalidOperationException("Сейчас не ваш ход.");

            int totalCount = DiceRolls.Values.SelectMany(dice => dice).Count(d => d == CurrentBet.Value.Number || d == 1);

            if (totalCount >= CurrentBet.Value.Count)
            {
                Players[_turnOrder.Peek()].LoseDie();
            }
            else
            {
                for (int i = 0; i < _turnOrder.Count - 1; i++)
                {
                    //Console.WriteLine($"---{_turnOrder.Peek()}");
                    var pl = _turnOrder.Dequeue();
                    _turnOrder.Enqueue(pl);
                }

                Players[_turnOrder.Peek()].LoseDie();
            }

            var loser = Players[_turnOrder.Peek()];
            int count = _turnOrder.Count;
            for (int i = 0; i < count; i++)
            {
                //Console.WriteLine($"*{_turnOrder.Peek()}");
                var pl = _turnOrder.Dequeue();
                if (Players[pl].DiceCount != 0)
                    _turnOrder.Enqueue(pl);
            }
            //Console.WriteLine($"+{_turnOrder.Peek()}");
            if (_turnOrder.Count == 1)
                GameStarted = false;

            Dictionary<string, int> diceCount = new();

            foreach (var player in Players)
            {
                diceCount[player.Name] = player.DiceCount;
            }
            return (diceCount, loser.Name, loser.Name != Players[_turnOrder.Peek()].Name);
        }
    }
    public class Player
    {
        public string Name { get; set; }
        public int DiceCount { get; set; } = 1;
        public Player(string name)
        {
            Name = name;
        }
        public void LoseDie()
        {
            if (DiceCount > 0)
                DiceCount--;
        }
    }
    public class PlayerRoll
    {
        public Player Player { get; set; }
        public int Roll { get; set; }
        public List<int> RollsHistory { get; set; } = new List<int>();
    }
}