using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBotCore.Model
{ 

    public class Lobby
    {
        public const int MAXPLAYERS = 8;
        public const int VOTETHRESHOLD = 5;

        public int LobbyID { get; set; }
        public League League { get; set; }
        public Player Host { get; set; }
        public List<Player> WaitingList { get; set; }
        public List<Player> CancelCalls { get; set; }
        public List<Player> RedWinCalls { get; set; }
        public List<Player> BlueWinCalls { get; set; }
        public List<Player> DrawCalls { get; set; }
        public bool HasStarted { get; set; }
        public bool IsClosed { get; set; }
        public Match Match { get; set; }
        public String Password { get;}
        public IUserMessage StartMessage { get; set; }
        

        public Lobby(Player host, League league) : this(league)
        {
            Host = host;
            WaitingList.Add(host);
        
        }

        public Lobby(League league)
        {
            League = league;
            HasStarted = false;
            WaitingList = new List<Player>();
            CancelCalls = new List<Player>();
            BlueWinCalls = new List<Player>();
            RedWinCalls = new List<Player>();
            DrawCalls = new List<Player>();
            Password = GeneratePassword(6);
        }


        public Player GetPlayerBySteamID(ulong steamID)
        {
            foreach (var player in WaitingList)
            {
                if (player.SteamID == steamID)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>
        /// Adds a player to the game.
        /// </summary>
        /// <param name="player">User who wishes to join.</param>
        /// <returns>True if successful. False if game is full. Null if player already present.</returns>
        public bool? AddPlayer(Player player)
        {
            if (WaitingList.Count == MAXPLAYERS)
                return false;
            else if (WaitingList.Contains(player))
                return null;

            WaitingList.Add(player);

            return true;
        }


        /// <summary>
        /// Removes User from game.
        /// </summary>
        /// <param name="user">User who wishes to leave.</param>
        /// <returns>True if sucessful. False if game is empty. Null if player not in game.</returns>
        public bool? RemovePlayer(Player user)
        {
            if (!WaitingList.Contains(user))
                return null;

            WaitingList.Remove(user);

            if (WaitingList.Count == 0)
                return false;

            if (user == Host)
                Host = WaitingList.First();

            return true;
        }

        /// <summary>
        /// Starts the game.
        /// </summary>
        public void StartGame(Match match)
        {
            Match = match;
            HasStarted = true;
        }

        /// <summary>
        /// Generates a random string of specified length using only alpha-numeric characters.
        /// </summary>
        /// <param name="length">Length of the string</param>
        /// <returns>String of random characters.</returns>
        public static String GeneratePassword(int length)
        {
            const String chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new String(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
