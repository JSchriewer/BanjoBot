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
            League league = lobby.League;
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

        public async Task SetMatchResult(Teams winner)
        {
            Winner = winner;
            List<Player> team1 = GetTeam(Teams.Blue);
            List<Player> team2 = GetTeam(Teams.Red);
            int mmrAdjustment = MatchMaker.CalculateMmrAdjustment(team1, team2,League.LeagueID,League.Season);

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

                PlayerMatchStats.Add(stats);
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
        /// <summary>
        /// Returns the average MMR of all players on the team.
        /// </summary>
        /// <param name="team">Can be either Blue or Red.</param>
        /// <returns>Average MMR as int.</returns>
        public int GetTeamMMR(Teams team)
        {
            return (int)MatchMaker.GetTeamMMR(GetTeam(team), League.LeagueID, League.Season);

        }

        public List<Player> GetTeam(Teams team)
        {
            return (List<Player>)from stats in PlayerMatchStats where stats.Team == Teams.Blue select stats.Player;
        }
    }
}
