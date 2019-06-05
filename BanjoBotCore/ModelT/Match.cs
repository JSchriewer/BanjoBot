using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public enum Teams { Blue, Red, Draw, None };

namespace BanjoBotCore.Model
{
    public class Match {
        public int MatchID { get; set; }
        public League League { get; set; }
        public int LeagueID { get; set; }
        public ulong SteamMatchID { get; set; }
        public int Season { get; set; }
        public Teams Winner { get; set; }  = Teams.None;
        public DateTime Date { get; set; }
        public int Duration { get; set; }
        public bool StatsRecorded { get; set; }
        public List<MatchPlayerStats> PlayerMatchStats { get; set; }

        // Database Constructor
        public Match(int matchID, int leagueID, ulong steamMatchID, int season, Teams winner, DateTime date, int duration, List<MatchPlayerStats> stats, bool statsRecorded)
        {
            MatchID = matchID;
            LeagueID = LeagueID;
            SteamMatchID = steamMatchID;
            Season = season;
            Winner = winner;
            Date = date;
            Duration = duration;
            PlayerMatchStats = stats;
            StatsRecorded = statsRecorded;
        }

        public Match(Lobby lobby) {
            //TODO: LeagueID, Season needed?
            League = lobby.League;
            LeagueID = lobby.League.LeagueID;
            SteamMatchID = 0;
            Season = lobby.League.Season;
            Date = DateTime.Now;
            Duration = 0;
            StatsRecorded = false;
            PlayerMatchStats = new List<MatchPlayerStats>();
            Tuple<List<Player>, List<Player>> teamOneAndTwo = MatchMaker.BalanceTeams(lobby.WaitingList, lobby.League);
            MatchPlayerStats stats = null;
            foreach (var player in teamOneAndTwo.Item1)
            {
                stats = new MatchPlayerStats(this, player, 0, 0, Teams.Blue, false);
                PlayerMatchStats.Add(stats);
            }
            foreach (var player in teamOneAndTwo.Item2)
            {
                stats = new MatchPlayerStats(this, player, 0, 0, Teams.Red, false);
                PlayerMatchStats.Add(stats);
            }
        }

        public async Task SetMatchResult(Teams winner, int mmrAdjustment)
        {
            Winner = winner;

            foreach (var stats in PlayerMatchStats)
            {
                if (Winner == Teams.None)
                {
                    stats.Win = false;
                }
                else if (Winner == Teams.Draw)
                {
                    stats.Win = false;
                }
                else if (stats.Team == winner)
                {
                    stats.Win = true;
                    stats.MmrAdjustment = mmrAdjustment;
                    stats.StreakBonus = 2 * stats.Player.GetLeagueStats(League.LeagueID, League.Season).Streak;
                }
                else
                {

                    stats.MmrAdjustment = -mmrAdjustment;
                    stats.StreakBonus = 0;
                    stats.Win = false;
                }
            }
        }

        public async Task<MatchPlayerStats> GetPlayerStats(Player player)
        {
            foreach(MatchPlayerStats stats in PlayerMatchStats)
            {
                if (stats.Player == player)
                    return stats;
            }

            return null;
        }

        public List<Player> GetTeam(Teams team)
        {
            List<Player> players = PlayerMatchStats.Where(s => s.Team == team).Select(s => s.Player).ToList<Player>();
            return players;
        }

        public List<Player> GetPlayers()
        {
            List<Player> players = PlayerMatchStats.Select(s => s.Player).ToList<Player>();
            return players;
        }
        
        public List<Player> GetWinnerTeam()
        {
            if (Winner == Teams.None || Winner == Teams.Draw)
            {
                return null;
            }

            if (Winner == Teams.Blue)
                return GetTeam(Teams.Blue);
            else
                return GetTeam(Teams.Red);
        }

        public List<Player> GetLoserTeam()
        {
            if (Winner == Teams.None || Winner == Teams.Draw)
            {
                return null;
            }

            if (Winner == Teams.Blue)
                return GetTeam(Teams.Red);
            else
                return GetTeam(Teams.Blue);
        }

        public int GetTeamMMR(Teams team)
        {
            List<Player> teamList = GetTeam(team);
            int averageMMR = 0;
            foreach (var player in teamList)
            {
                averageMMR += player.GetLeagueStats(League.LeagueID, Season).MMR;
            }

            if (teamList.Count > 0)
            {
                averageMMR = averageMMR / teamList.Count;
            }



            return averageMMR;
        }
    }
}
