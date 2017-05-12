using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.API;
using Discord.WebSocket;

namespace BanjoBot
{
    public class Player : IEquatable<Player> {
        public SocketGuildUser User { get; set; }
        public ulong SteamID { get; set; }
        public Lobby CurrentGame   { get; set; }
        public List<PlayerStats> PlayerStats { get; set; }
        public List<PlayerMatchStats> Matches { get; set; }

        public Player(SocketGuildUser discordUser, ulong steamid)
        {
            User = discordUser;
            SteamID = steamid;
            CurrentGame = null;
            PlayerStats = new List<PlayerStats>();
            Matches = new List<PlayerMatchStats>();
        }

        public bool Equals(Player other)
        {
            return User.Id == other.User.Id;
        }

        public string PlayerMMRString(int leagueID, int season)
        {
            return User.Username + "(" + GetLeagueStat(leagueID, season).MMR + ")";
        }

        public PlayerStats GetLeagueStat(int leagueID, int season) {
            foreach (var leagueStat in PlayerStats) {
                if (leagueStat.LeagueID == leagueID && leagueStat.Season == season)
                    return leagueStat;
            }
            return null;
        }

        public List<PlayerMatchStats> GetMatchesBySeason(int leagueID, int season)
        {
            List<PlayerMatchStats> result = new List<PlayerMatchStats>();
            foreach (PlayerMatchStats ps in Matches)
            {
                if (ps.Match.LeagueID == leagueID && ps.Match.Season == season)
                {
                    result.Add(ps);
                }
            }

            return result;
        }

        public List<PlayerMatchStats> GetAllMatches(int leagueID) {
            List<PlayerMatchStats> result = new List<PlayerMatchStats>();
            foreach (PlayerMatchStats ps in Matches) {
                if (ps.Match.LeagueID == leagueID) {
                    result.Add(ps);
                }
            }

            return result;
        }

        public PlayerMatchStats GetMatchStats(MatchResult match)
        {
            foreach (var stats in Matches)
            {
                if (stats.Match == match)
                {
                    return stats;
                }
            }

            return null;
        }

        public void IncMatches(int leagueID, int season)
        {
            GetLeagueStat(leagueID, season).MatchCount++;
        }

        public void IncMMR(int leagueID, int season, int mmr) {
            GetLeagueStat(leagueID, season).MMR += mmr;
        }

        public void SetMMR(int leagueID, int season, int mmr) {
            GetLeagueStat(leagueID, season).MMR = mmr;
        }

        public void DecMMR(int leagueID, int season, int mmr) {
            GetLeagueStat(leagueID, season).MMR -= mmr;
        }

        public void IncWins(int leagueID, int season)
        {
            GetLeagueStat(leagueID, season).Wins++;
        }

        public void IncLosses(int leagueID, int season)
        {
            GetLeagueStat(leagueID, season).Losses++;
        }
        public void IncStreak(int leagueID, int season)
        {
            GetLeagueStat(leagueID, season).Streak++;
        }
        public void SetStreakZero(int leagueID, int season) {
            GetLeagueStat(leagueID, season).Streak = 0;
        }
        public bool IsIngame()
        {
            return CurrentGame != null;
        }

    }
}
