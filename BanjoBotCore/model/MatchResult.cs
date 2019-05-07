using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BanjoBot {
    public class MatchResult {
        public int MatchID { get; set; }
        public int LeagueID { get; set; }
        public ulong SteamMatchID { get; set; }
        public int Season { get; set; }
        public Teams Winner { get; set; }  = Teams.None;
        public DateTime Date { get; set; }
        public int Duration { get; set; }
        public bool StatsRecorded { get; set; }
        public List<PlayerMatchStats> PlayerMatchStats { get; set; }  

        // Database Constructor
        public MatchResult(int matchID, int leagueID, ulong steamMatchID, int season, Teams winner, DateTime date, int duration, List<PlayerMatchStats> stats, bool statsRecorded)
        {
            MatchID = matchID;
            LeagueID = leagueID;
            SteamMatchID = steamMatchID;
            Season = season;
            Winner = winner;
            Date = date;
            Duration = duration;
            PlayerMatchStats = stats;
            StatsRecorded = statsRecorded;
        }
        // Json Constructor
        public MatchResult()
        {
            
        }
        // Json Constructor
        //[JsonConstructor]
        //public MatchResult(int matchID, int leagueID, ulong steamMatchID, int season, Teams winner, int duration, List<PlayerMatchStats> stats) {
        //    MatchID = matchID;
        //    LeagueID = leagueID;
        //    SteamMatchID = steamMatchID;
        //    Season = season;
        //    Winner = winner;
        //    Date = DateTime.Now;
        //    Duration = duration;
        //    PlayerMatchStats = stats;
        //    StatsRecorded = true;
        //}

        // Vote Constructor
        public MatchResult(Lobby game) {
            // Manually closed by vote or moderator
            MatchID = game.MatchID;
            LeagueID = game.League.LeagueID;
            SteamMatchID = 0;
            Season = game.League.Season;
            Winner = game.Winner;
            Date = DateTime.Now;
            Duration = 0;
            StatsRecorded = false;
            PlayerMatchStats = new List<PlayerMatchStats>();

            PlayerMatchStats stats = null;
            foreach (var player in game.WaitingList) {
                if ((game.BlueList.Contains(player) && game.Winner == Teams.Blue) ||
                    (game.RedList.Contains(player) && game.Winner == Teams.Red))
                {
                    stats = new PlayerMatchStats(this, player.SteamID, 0, 0, Winner, true);
                }
                else
                {
                    stats = new PlayerMatchStats(this, player.SteamID, 0, 0, Winner, false);
                }
                PlayerMatchStats.Add(stats);
            }
        }
    }
}
