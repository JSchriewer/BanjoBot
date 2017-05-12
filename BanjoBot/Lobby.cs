using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBot
{
    /// <summary>
    /// Enumerator for referring to teams.
    /// </summary>
    public enum Teams { Blue, Red, Draw, None};

    public class Lobby
    {
        //TODO: persistent Lobby, Add Date
        // Constants
        public const int MAXPLAYERS    = 8;
        public const int VOTETHRESHOLD = 5;
                
        // Props
        public League League { get; set; }
        public Player Host                { get; set; }
        public List<Player> WaitingList   { get; set; }
        public List<Player> RedList       { get; set; }
        public List<Player> BlueList      { get; set; }
        public Teams Winner             { get; set; }
        public List<Player> CancelCalls   { get; set; }
        public List<Player> RedWinCalls   { get; set; }  
        public List<Player> BlueWinCalls  { get; set; }
        public List<Player> DrawCalls     { get; set; }
        public int MmrAdjustment { get; set; }
        public bool HasStarted { get; set; }
        public int MatchID { get; set; }
        public int GameNumber { get; set; }
        public IUserMessage StartMessage { get; set; }

        /// <summary>
        /// Game constructor. Queries database for game name and binds Host to game.
        /// </summary>
        /// <param name="host">User who hosted the game.</param>
        public Lobby(Player host, League league)
        {
            GameNumber = 0;
            Host     = host;
            League = league;
            HasStarted = false;
            WaitingList   = new List<Player>();
            RedList       = new List<Player>();
            BlueList      = new List<Player>();
            CancelCalls   = new List<Player>();
            BlueWinCalls  = new List<Player>();
            RedWinCalls   = new List<Player>();
            DrawCalls     = new List<Player>();
            WaitingList.Add(host);
        }

        public Lobby(League league) {
            GameNumber = 0;
            League = league;
            HasStarted = false;
            WaitingList = new List<Player>();
            RedList = new List<Player>();
            BlueList = new List<Player>();
            CancelCalls = new List<Player>();
            BlueWinCalls = new List<Player>();
            RedWinCalls = new List<Player>();
            DrawCalls = new List<Player>();
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

            if(user == Host)
                Host = WaitingList.First();

            return true;
        }

        /// <summary>
        /// Starts the game.
        /// </summary>
        public void StartGame()
        {
            AssignTeams();
            HasStarted = true;
        }

        public int CalculateMmrAdjustment()
        {
            double mmrDifference = 0;
            if (Winner == Teams.Blue)
            {
                mmrDifference = GetTeamMMR(Teams.Blue) - GetTeamMMR(Teams.Red);
            } else
            {
                mmrDifference = GetTeamMMR(Teams.Red) - GetTeamMMR(Teams.Blue);
            }
          
            return mmrCurve(mmrDifference);
        }

        public int mmrCurve(double x) {
            double baseMMR = 25;
            double approaches = 10;
            double approachRate = Math.Atan(-x * (1 / 350.0));
            double result = approachRate * approaches + baseMMR;
            return Convert.ToInt32(result);
        }


        /// <summary>
        /// Assigns players in waiting list to teams of roughly equal MMR.
        /// By Kael
        /// </summary>
        public void AssignTeams()
        {
            var numPlayers = WaitingList.Count;
            var mmrs = new List<int>();
            var subsets = TryCombinations(numPlayers);
            var storedTeams = new List<int>();
            var bestMmrDiff = double.PositiveInfinity;

            foreach (var s in subsets)
            {
                var mmr1 = 0;
                var mmr2 = 0;

                for (int i=0; i<numPlayers; i++)
                {
                    if (s[i] == 1)
                        mmr1 += WaitingList[i].GetLeagueStat(League.LeagueID,League.Season).MMR;
                    else
                        mmr2 += WaitingList[i].GetLeagueStat(League.LeagueID, League.Season).MMR;
                }

                var difference = Math.Abs(mmr1 - mmr2);

                if (difference < bestMmrDiff)
                {
                    bestMmrDiff = difference;
                    storedTeams = s;
                }
            }

            // Assign the players to teams
            for (int i=0; i<numPlayers; i++)
            {
                if (storedTeams[i] == 1)
                    RedList.Add(WaitingList[i]);
                else
                    BlueList.Add(WaitingList[i]);
            }
        }

        /// <summary>
        /// By Kael
        /// </summary>
        /// <param name="numPlayers">Number of players in the game.</param>
        /// <returns></returns>
        public List<List<int>> TryCombinations(int numPlayers)
        {
            var output = new List<List<int>>();
            output.Add(new List<int>());
            output.Add(new List<int>());
            output[0].Add(1);
            output[1].Add(0);

            for (int i = 1; i < numPlayers; i++)
            {
                var count = output.Count;
                for (int oIndex = 0; oIndex < count; oIndex++)
                {
                    var o = output[oIndex];
                    var copy = o.Select(v => v).ToList();
                    o.Add(0);
                    copy.Add(1);
                    output.Add(copy);
                }
            }

            var freshOutput = new List<List<int>>();

            foreach (var o in output)
            {
                var sum = o.Sum();
                if (sum < numPlayers / 2 || sum > (numPlayers / 2) + 1)
                    continue;
                freshOutput.Add(o);
            }

            return freshOutput;
        }

        /// <summary>
        /// Returns the average MMR of all players on the team.
        /// </summary>
        /// <param name="team">Can be either Blue or Red.</param>
        /// <returns>Average MMR as int.</returns>
        public int GetTeamMMR(Teams team)
        {
            int averageMMR = 0;

            if(team == Teams.Red && RedList.Count() > 0)
            {
                foreach (var user in RedList)
                {
                    averageMMR += user.GetLeagueStat(League.LeagueID,League.Season).MMR;
                }
                averageMMR = averageMMR / RedList.Count();
            }
            else if (team == Teams.Blue && BlueList.Count() > 0)
            {
                foreach (var user in BlueList)
                {
                    averageMMR += user.GetLeagueStat(League.LeagueID,League.Season).MMR;
                }
                averageMMR = averageMMR / BlueList.Count();
            }

            return averageMMR;
        }

        public string GetGameName()
        {
            return "BBL#" + GameNumber; 
        }

        /// <summary>
        /// Generates a random string of specified length using only alpha-numeric characters.
        /// </summary>
        /// <param name="length">Length of the string</param>
        /// <returns>String of random characters.</returns>
        public static String GeneratePassword(int length) {
            const String chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new String(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
