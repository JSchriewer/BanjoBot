using BanjoBotCore.Controller;
using BanjoBotCore.Model;
using Discord;
using Discord.WebSocket;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BanjoBotCore
{
    public class LeagueController
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(LeagueController));

        private event EventHandler<SeasonEventArgs> SeasonEnded;
        public League League { get; }
        public LobbyController LobbyController { get; }
        private DatabaseController _database;

        public LeagueController(League league)
        {
            League = league;
            LobbyController = new LobbyController();
            _database = new DatabaseController();
        }

        public async Task RegisterEventListener(ILeagueEventListener listener)
        {
            SeasonEnded += listener.SeasonEnded;
            await LobbyController.RegisterEventListener(listener);
        }

        public async Task<List<Player>> GetLeaderBoard()
        {
            List<Player> players = await GetActivePlayers();
            return players.OrderBy(player => player.GetLeagueStats(League.LeagueID, League.Season).MMR).Reverse().ToList();
        }

        public async Task<List<Player>> GetLeaderBoard(int season)
        {
            List<Player> players = await GetActivePlayers();
            return players.OrderBy(player => player.GetLeagueStats(League.LeagueID, season).MMR).Reverse().ToList();
        }

        public async Task<List<Player>> GetActivePlayers()
        {
            //TODO: GetActivePlayers
            //return context.Players.Where();
            return new List<Player>();
        }

        public async Task StartNewSeason()
        {
            //int season = League.Season;

            //foreach (Player player in League.RegisteredPlayers)
            //{
            //    PlayerStats newStats = new PlayerStats(League.LeagueID, League.Season + 1);
            //    player.PlayerStats.Add(newStats);
            //    await _database.UpdatePlayerStats(player, newStats);
            //}

            //League.Season++;
            //League.Matches = new List<Match>();
            //await _database.UpdateLeague(League);

            //await OnSeasonEnded(League, season, GetLeaderBoard(season));
        }

        public async Task SetLeagueChannel(SocketGuildChannel socketGuildChannel)
        {
            League.LeagueDiscordConfig.LeagueChannel = socketGuildChannel;
            await _database.UpdateLeague(League);
        }

        public async Task SetModRole(SocketRole role)
        {
            League.LeagueDiscordConfig.ModeratorRole = role;
            await _database.UpdateLeague(League);
        }

        protected async Task OnSeasonEnded(League league, int season, List<Player> leaderBoard)
        {
            EventHandler<SeasonEventArgs> handler = SeasonEnded;
            SeasonEventArgs args = new SeasonEventArgs();
            args.League = league;
            args.Season = season;
            args.LeaderBoard = leaderBoard;

            handler?.Invoke(this, args);
        }
    }

    public class LeagueEventArgs : EventArgs
    {
        public League League { get; set; }
    }

    public class SeasonEventArgs : LeagueEventArgs
    {
        public int Season { get; set; }
        public List<Player> LeaderBoard { get; set; }
    }

    [Serializable]
    public class InsufficientPermissionException : Exception
    {
        public InsufficientPermissionException(string message) : base(message)
        {
        }
    }

    public class LeagueException : Exception
    {
        public LeagueException(string message) : base(message)
        {
        }
    }
}