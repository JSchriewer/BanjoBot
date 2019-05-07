using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBot {
    public class PlayerStats {
        public int LeagueID { get; set; }
        public int Season { get; set; }
        public int MatchCount { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int MMR { get; set; }
        public int Streak { get; set; }

        public PlayerStats(int leagueID, int season, int matchCount = 0, int wins = 0, int losses = 0, int mmr = 1000, int streak = 0)
        {
            LeagueID = leagueID;
            Season = season;
            MatchCount = matchCount;
            Wins = wins;
            Losses = losses;
            MMR = mmr;
            Streak = streak;
        }

        public void ResetToSeason(int season)
        {
            Season = season;
            MatchCount = 0;
            Wins = 0;
            Losses = 0;
            MMR = 1000;
            Streak = 0;
        }
    }
}
