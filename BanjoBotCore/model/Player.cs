﻿using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.API;
using Discord.WebSocket;

namespace BanjoBotCore
{
    public class Player : IEquatable<Player> {
        public SocketGuildUser User { get; set; }
        public ulong discordID { get; }
        public ulong SteamID { get; set; }
        public Lobby CurrentGame   { get; set; }
        public List<PlayerStats> PlayerStats { get; set; }
        public List<MatchResult> Matches { get; set; }

        public Player(SocketGuildUser discordUser, ulong steamid)
        {
            User = discordUser;
            SteamID = steamid;
            CurrentGame = null;
            PlayerStats = new List<PlayerStats>();
            Matches = new List<MatchResult>();
            discordID = User.Id;
        }

        public Player(ulong steamid) {
            SteamID = steamid;
            CurrentGame = null;
            PlayerStats = new List<PlayerStats>();
            Matches = new List<MatchResult>();
        }


        public Player(ulong discord_id, ulong steamid)
        {
            discordID = discord_id;
            SteamID = steamid;
            CurrentGame = null;
            PlayerStats = new List<PlayerStats>();
            Matches = new List<MatchResult>();
        }


        public bool Equals(Player other)
        {
            return SteamID == other.SteamID;
        }

        public string PlayerMMRString(int leagueID, int season)
        {

            return Name + "(" + GetLeagueStat(leagueID, season).MMR + ")";

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

        public PlayerStats GetLeagueStat(int leagueID, int season) {
            foreach (var leagueStat in PlayerStats) {
                if (leagueStat.LeagueID == leagueID && leagueStat.Season == season)
                    return leagueStat;
            }

            //Bug workaround !top command causes nullpointer when no stats for current season are recorded
            PlayerStats stats = new PlayerStats(leagueID, season);
            PlayerStats.Add(new PlayerStats(leagueID, season));
            return stats;
        }

        public List<MatchResult> GetMatchesBySeason(int leagueID, int season)
        {
            List<MatchResult> result = new List<MatchResult>();
            foreach (MatchResult match in Matches)
            {
                if (match.LeagueID == leagueID && match.Season == season)
                {
                    result.Add(match);
                }
            }

            return result;
        }

        public List<MatchResult> GetAllMatches(int leagueID) {
            List<MatchResult> result = new List<MatchResult>();
            foreach (MatchResult match in Matches) {
                if (match.LeagueID == leagueID) {
                    result.Add(match);
                }
            }

            return result;
        }

        public MatchPlayerStats GetMatchStats(MatchResult match)
        {
            foreach (var stats in match.PlayerMatchStats)
            {
                if (stats.Player == this)
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
        public bool IsIngame
        {
            get{ return CurrentGame != null; }
        }

    }
}
