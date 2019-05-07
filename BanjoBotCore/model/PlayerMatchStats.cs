using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BanjoBot {
    public class PlayerMatchStats {
        public MatchResult Match { get; set; }
        public ulong SteamID { get; set; }
        public int HeroID { get; set; }
        public int Goals { get; set; }
        public int Assist { get; set; }
        public int Steals { get; set; }
        public int Turnovers { get; set; }
        public int StealTurnDif { get; set; }
        public int Pickups { get; set; }
        public int Passes { get; set; }
        public int PassesReceived { get; set; }
        public float SaveRate { get; set; }
        public int Points { get; set; }
        public int PossessionTime { get; set; }
        public int TimeAsGoalie { get; set; }
        public int MmrAdjustment { get; set; }
        public int StreakBonus { get; set; }
        public Teams Team { get; set; }
        public bool Win { get; set; }

        public PlayerMatchStats(MatchResult match, ulong steamId, int hero, int goals, int assist, int steals, int turnovers, int stealTurnDif, int pickups, int passes, int passesReceived, float saveRate, int points, int possessionTime, int timeAsGoalie, int mmrAdjustment, int streakBonus, Teams team, bool win) {
            Match = match;
            SteamID = steamId;
            HeroID = hero;
            Goals = goals;
            Assist = assist;
            Steals = steals;
            Turnovers = turnovers;
            StealTurnDif = stealTurnDif;
            Pickups = pickups;
            Passes = passes;
            PassesReceived = passesReceived;
            SaveRate = saveRate;
            Points = points;
            PossessionTime = possessionTime;
            TimeAsGoalie = timeAsGoalie;
            MmrAdjustment = mmrAdjustment;
            StreakBonus = streakBonus;
            Team = team;
            Win = win;
        }

        //For Json
        public PlayerMatchStats() { 
        }

        //For Voting
        public PlayerMatchStats(MatchResult match, ulong steamId, int mmrAdjustment, int streakBonus, Teams team, bool win) {
            Match = match;
            SteamID = steamId;
            HeroID = -1;
            Goals = 0;
            Assist = 0;
            Steals = 0;
            Turnovers = 0;
            StealTurnDif = 0;
            Pickups = 0;
            Passes = 0;
            PassesReceived = 0;
            SaveRate = 0;
            Points = 0;
            PossessionTime = 0;
            TimeAsGoalie = 0;
            MmrAdjustment = mmrAdjustment;
            StreakBonus = streakBonus;
            Team = team;
            Win = win;
        }
    }
}
