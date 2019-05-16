using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.API;
using Discord.WebSocket;

namespace BanjoBotCore.Model
{
    public class Player : IEquatable<Player> {
        public SocketGuildUser User { get; set; }
        public ulong discordID { get; }
        public ulong SteamID { get; set; }
        public Lobby CurrentGame   { get; set; }
        public List<PlayerStats> PlayerStats { get; set; }
        public List<Match> Matches { get; set; }

        public Player(SocketGuildUser discordUser, ulong steamid)
        {
            User = discordUser;
            SteamID = steamid;
            CurrentGame = null;
            PlayerStats = new List<PlayerStats>();
            Matches = new List<Match>();
            discordID = User.Id;
        }

        public Player(ulong steamid) {
            SteamID = steamid;
            CurrentGame = null;
            PlayerStats = new List<PlayerStats>();
            Matches = new List<Match>();
        }


        public Player(ulong discord_id, ulong steamid)
        {
            discordID = discord_id;
            SteamID = steamid;
            CurrentGame = null;
            PlayerStats = new List<PlayerStats>();
            Matches = new List<Match>();
        }

        public bool Equals(Player other)
        {
            return SteamID == other.SteamID;
        }

        public string PlayerMMRString(int leagueID, int season)
        {

            return Name + "(" + GetLeagueStats(leagueID, season).MMR + ")";

        }

        public string Name
        {
            get
            {
                if (User.Nickname == null)
                    return User.Username;
                else
                    return User.Nickname;
            }
        }

        public PlayerStats GetLeagueStats(int leagueID, int season) {
            foreach (var leagueStat in PlayerStats) {
                if (leagueStat.LeagueID == leagueID && leagueStat.Season == season)
                    return leagueStat;
            }

            //Return value should not be null
            PlayerStats stats = new PlayerStats(leagueID, season);
            PlayerStats.Add(new PlayerStats(leagueID, season));
            return stats;
        }

        public List<Match> GetMatchesBySeason(int leagueID, int season)
        {
            List<Match> result = new List<Match>();
            foreach (Match match in Matches)
            {
                if (match.LeagueID == leagueID && match.Season == season)
                {
                    result.Add(match);
                }
            }

            return result;
        }

        public List<Match> GetAllMatches(int leagueID) {
            List<Match> result = new List<Match>();
            foreach (Match match in Matches) {
                if (match.LeagueID == leagueID) {
                    result.Add(match);
                }
            }

            return result;
        }

        public void IncMatches(int leagueID, int season)
        {
            GetLeagueStats(leagueID, season).MatchCount++;
        }

        public void IncMMR(int leagueID, int season, int mmr) {
            GetLeagueStats(leagueID, season).MMR += mmr;
        }

        public void SetMMR(int leagueID, int season, int mmr) {
            GetLeagueStats(leagueID, season).MMR = mmr;
        }

        public void DecMMR(int leagueID, int season, int mmr) {
            GetLeagueStats(leagueID, season).MMR -= mmr;
        }

        public void IncWins(int leagueID, int season)
        {
            GetLeagueStats(leagueID, season).Wins++;
        }

        public void IncLosses(int leagueID, int season)
        {
            GetLeagueStats(leagueID, season).Losses++;
        }
        public void IncStreak(int leagueID, int season)
        {
            GetLeagueStats(leagueID, season).Streak++;
        }
        public void SetStreakZero(int leagueID, int season) {
            GetLeagueStats(leagueID, season).Streak = 0;
        }
        public bool IsIngame
        {
            get{ return CurrentGame != null; }
        }

    }
}
