using BanjoBotCore.Controller;
using BanjoBotCore.Model;
using Discord;
using Discord.WebSocket;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BanjoBotCore
{
    //TODO: throw Exception in Remove/AddPLayer() <- bool? removePlayerResult = Lobby.RemovePlayer(user);
    //->Duplicated code LeavePlayer / KickPlayer
    //TODO: Don't catch database exceptions
    //->Use try{}finally{} for error handling wherever possible
    // Eventargs should be immutable -> implement ICloneable in Model classes or use DTOs later

    public class LeagueController
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(LeagueController));

        private event EventHandler<RegistrationEventArgs> PlayerRegistrationAccepted;

        private event EventHandler<RegistrationEventArgs> PlayerRegistered;

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
            PlayerRegistrationAccepted += listener.PlayerRegistrationAccepted;
            PlayerRegistered += listener.PlayerRegistered;
            SeasonEnded += listener.SeasonEnded;
            await LobbyController.RegisterEventListener(listener);
        }

        public async Task<Player> RegisterPlayer(SocketGuildUser user, ulong steamID)
        {
            log.Debug("Creating new player");
            Player player = new Player(user, steamID);
            try
            {
                await _database.InsertPlayer(player);
            }
            catch (Exception e)
            {
                throw e;
            }

            await RegisterPlayer(player);
            return player;
        }

        public async Task RegisterPlayer(Player player)
        {
            if (League.DiscordInformation.AutoAccept)
            {
                await AcceptRegistration(player);
            }
            else
            {
                log.Debug("Add applicant" + player.User.Username + " to " + League.Name);
                try
                {
                    await _database.InsertSignupToLeague(player.SteamID, League);
                    League.Applicants.Add(player);
                    await OnPlayerRegistered(player, League);
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
        }

        public async Task AcceptRegistration(Player player)
        {
            log.Debug("RegisterPlayer: " + player.Name + "(" + player.User.Id + ")");

            try
            {
                player.PlayerStats.Add(new PlayerStats(League.LeagueID, League.Season));
                await _database.InsertRegistrationToLeague(player, League);
                await _database.UpdatePlayerStats(player, player.GetLeagueStats(League.LeagueID, League.Season));
            }
            catch (Exception e)
            {
                throw e;
            }

            if (League.Applicants.Contains(player))
            {
                League.Applicants.Remove(player);
            }

            League.RegisteredPlayers.Add(player);
            await OnPlayerRegistrationAccepted(player);
        }

        public async Task RemovePlayerFromLeague(Player player)
        {
            League.Applicants.Remove(player);
            await _database.DeleteRegistration(player.SteamID, League);
        }

        public async Task StartNewSeason()
        {
            int season = League.Season;

            foreach (Player player in League.RegisteredPlayers)
            {
                PlayerStats newStats = new PlayerStats(League.LeagueID, League.Season + 1);
                player.PlayerStats.Add(newStats);
                await _database.UpdatePlayerStats(player, newStats);
            }

            League.Season++;
            League.Matches = new List<Match>();
            League.GameCounter = 0;
            await _database.UpdateLeague(League);

            await OnSeasonEnded(League, season);
        }

        public async Task SetModChannel(IChannel modChannel)
        {
            League.DiscordInformation.ModeratorChannel = (SocketGuildChannel)modChannel;
            await _database.UpdateLeague(League);
        }

        public async Task SetAutoAccept(Boolean autoAccept)
        {
            League.DiscordInformation.AutoAccept = autoAccept;
            await _database.UpdateLeague(League);
        }

        public async Task SetSteamRegister(Boolean steamRegister)
        {
            League.DiscordInformation.NeedSteamToRegister = steamRegister;
            await _database.UpdateLeague(League);
        }

        public async Task SetChannel(SocketGuildChannel socketGuildChannel)
        {
            League.DiscordInformation.Channel = (SocketGuildChannel)socketGuildChannel;
            await _database.UpdateLeague(League);
        }

        public async Task SetLeagueRole(SocketRole role)
        {
            League.DiscordInformation.LeagueRole = role;
            await _database.UpdateLeague(League);
        }

        public async Task SetModRole(SocketRole role)
        {
            League.DiscordInformation.ModeratorRole = role;
            await _database.UpdateLeague(League);
        }

        protected async Task OnPlayerRegistered(Player player, League league)
        {
            EventHandler<RegistrationEventArgs> handler = PlayerRegistered;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;
            args.League = league;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerRegistrationAccepted(Player player)
        {
            EventHandler<RegistrationEventArgs> handler = PlayerRegistrationAccepted;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;
            args.League = League;

            handler?.Invoke(this, args);
        }

        protected async Task OnSeasonEnded(League league, int season)
        {
            EventHandler<SeasonEventArgs> handler = SeasonEnded;
            SeasonEventArgs args = new SeasonEventArgs();
            args.League = league;
            args.Season = season;

            handler?.Invoke(this, args);
        }
    }

    public class LeagueEventArgs : EventArgs
    {
        public League League { get; set; }
    }

    public class RegistrationEventArgs : LeagueEventArgs
    {
        public Player Player { get; set; }
    }

    public class SeasonEventArgs : LeagueEventArgs
    {
        public int Season { get; set; }
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