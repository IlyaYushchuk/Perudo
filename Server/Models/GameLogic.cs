using System;
using System.Collections.Generic;
using System.Linq;

namespace PerudoGame.Server.Models
{
    public class GameLogic
    {
        // Список игроков в игре
        public List<Player> Players { get; set; } = new List<Player>();
        public int CurrentPlayerIndex { get; set; } = 0; // Индекс текущего игрока
        public string? CurrentBid { get; set; } // Последняя ставка
        public bool GameStarted { get; private set; } = false; // Статус игры

        public int ReadyPlayers { get; set; } = 0;

        // Добавление нового игрока
        public void AddPlayer(string name)
        {
            //Console.WriteLine(name);
            if (GameStarted) throw new InvalidOperationException("Игра уже началась.");
            Players.Add(new Player(name));
            Console.WriteLine(Players.Count);
        }

        // Запуск игры
        public bool StartGame()
        {
            if (Players.Count < 2) throw new InvalidOperationException("Для начала игры необходимо минимум два игрока.");

            ReadyPlayers++;

            if(Players.Count == ReadyPlayers)
            {
                GameStarted = true;
                return true;
            }
            else
            {
                return false;
            }
        }

        public int FirstPlayerChoice()
        {
            //TODO redo
            return 0;
        }

        // Совершение ставки
        public void MakeBid(string playerName, string bid)
        {
            if (!GameStarted) throw new InvalidOperationException("Игра не началась.");
            if (Players[CurrentPlayerIndex].Name != playerName)
                throw new InvalidOperationException("Сейчас не ход этого игрока.");

            CurrentBid = bid;
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count; // Переход хода
        }
    }

    // Класс игрока
    public class Player
    {
        public string Name { get; set; } // Имя игрока
        public List<int> Dice { get; set; } = new List<int>(); // Список кубиков игрока

        public Player(string name)
        {
            Name = name;
        }

        // Функция броска кубиков
        public void RollDice()
        {
            var random = new Random();
            Dice = Enumerable.Range(0, 5).Select(_ => random.Next(1, 7)).ToList(); // Бросок 5 кубиков
        }
    }
}
