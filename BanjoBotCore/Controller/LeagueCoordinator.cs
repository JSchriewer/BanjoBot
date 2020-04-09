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
    //TODO: Remove Registration and add RegistrationService
    public class LeagueCoordinator
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(LeagueController));
        private static readonly int PUBLIC_LEAGUE_ID = 25;

        private event EventHandler<RegistrationEventArgs> PlayerRegistrationAccepted;
        private event EventHandler<RegistrationEventArgs> PlayerRegistrationFailed;
        private event EventHandler<RegistrationEventArgs> PlayerRegistered;

        private DatabaseController _database;

        public DiscordServer DiscordServer { get; set; }
        public List<LeagueController> LeagueControllers { get; set; } = new List<LeagueController>();
        public List<Player> Players
        {
            set { Players = value; }
            get { return Players.Where(p => p.Approved).ToList(); }
        }

        public List<Player> Applicants
        {
            get { return Players.Where(p => !p.Approved).ToList(); }
        }

        public LeagueCoordinator()
        {
            _database = new DatabaseController();
            Players = new List<Player>();
            
        }

        public async Task RegisterEventListener(ILeagueEventListener listener)
        {
            PlayerRegistrationAccepted += listener.PlayerRegistrationAccepted;
            PlayerRegistrationFailed += listener.PlayerRegistrationFailed;
            PlayerRegistered += listener.PlayerRegistered;
            foreach (var lc in LeagueControllers)
            {
                await lc.RegisterEventListener(listener);
            }
        }

        public async Task CreateLeague(String name, LeagueDiscordConfig discordInfo)
        {
            log.Debug("Create new league (Name = " + name + " )");

            int leagueID = await _database.InsertLeague();
            League league = new League(leagueID, name, 1);
            league.LeagueDiscordConfig = discordInfo;

            await AddLeague(league);
            await _database.UpdateLeague(league);
        }

        public async Task DeleteLeague(LeagueController lc)
        {
            await _database.DeleteLeague(lc.League);
            LeagueControllers.Remove(lc);
        }

        public async Task<LeagueController> AddLeague(League league)
        {
            LeagueController newLeague = new LeagueController(league);
            LeagueControllers.Add(newLeague);
            return newLeague;
        }

        public async Task AddLeagues(List<League> leagues)
        {
            foreach (var league in leagues)
            {
                await AddLeague(league);
            }
        }

        public async Task AddLeague(League league, ILeagueEventListener listener)
        {
            LeagueController lc = await AddLeague(league);
            await lc.RegisterEventListener(listener);
        }

        public async Task AddLeagues(List<League> leagues, ILeagueEventListener listener)
        {
            foreach (var league in leagues)
            {
                await AddLeague(league, listener);
            }
        }

        public LeagueController GetLeagueController(SocketGuildChannel channel)
        {
            return LeagueControllers.Find(lc => lc.League.LeagueDiscordConfig?.LeagueChannel == channel);
        }

        public LeagueController GetLeagueController(int leagueID)
        {
            return LeagueControllers.Find(lc => lc.League.LeagueID == leagueID);
        }

        public LeagueController GetPublicLeague()
        {
            return LeagueControllers.Find(l => l.League.LeagueID == PUBLIC_LEAGUE_ID);
        }

        public Player GetPlayerByDiscordID(ulong discordID)
        {
            return Players.Find(p => p.discordID == discordID);
        }

        public Player GetApplicantByDiscordID(ulong discordID)
        {
            return Applicants.Find(p => p.discordID == discordID);
        }

        public Player GetPlayerBySteamID(ulong steamID)
        {
            return Players.Find(p => p.SteamID == steamID);
        }

        public Player GetApplicantBySteamID(ulong steamID)
        {
            return Applicants.Find(p => p.SteamID == steamID);
        }

        public async Task<Player> RegisterPlayer(SocketGuildUser socketGuildUser, ulong steamID)
        {
            await CheckRegistration(socketGuildUser, steamID);

            log.Debug($"Creating new player SteamID({steamID})");
            Player player = new Player(socketGuildUser, steamID);
            Players.Add(player);
            await _database.InsertPlayer(player);

            if (DiscordServer.AutoAccept)
            {
                await AcceptRegistration(player);
            }
            else
            {
                log.Debug($"Add applicant {player.User.Username}");
                //TODO: DBA await _database.InsertSignup(player.SteamID, League);
                await OnPlayerRegistered(player);
            }

            return player;
        }

        private async Task CheckRegistration(SocketGuildUser socketGuildUser, ulong steamID)
        {
            Player player = GetPlayerByDiscordID(socketGuildUser.Id);
            if (player != null)
            {
                throw new LeagueException("You are already registered");
            }

            player = GetApplicantByDiscordID(socketGuildUser.Id);
            if (player != null)
            {
                throw new LeagueException("You are already signed up, wait for the approval by a moderator");
            }

            log.Debug("Registration: SteamCheck");
            if (DiscordServer.NeedSteamToRegister)
            {
                if (steamID == 0)
                {
                    throw new LeagueException("Missing steamID. Please use !register <YourSteamID64>");
                }

                if (!steamID.ToString().StartsWith("7656"))
                {
                    throw new LeagueException("Thats not a valid steamid64, please follow the instructions in #welcome");
                }
                foreach (var regplayer in Players)
                {
                    if (regplayer.SteamID == steamID && regplayer.discordID != socketGuildUser.Id)
                    {
                        throw new LeagueException("The SteamID is already in use, please contact a moderator");
                    }
                }
            }

            log.Debug("Registrationdata of " + socketGuildUser.Username + " is valid");
        }

        public async Task AcceptRegistration(Player player)
        {
            log.Debug("RegisterPlayer: " + player.Name + "(" + player.User.Id + ")");

            foreach (var lc in LeagueControllers)
            {
                player.PlayerStats.Add(new PlayerStats(lc.League.LeagueID, lc.League.Season));
            }

            //TODO: DBA await _database.InsertRegistrationToLeague(player, League);
            //TODO: DBA await _database.UpdatePlayerStats(player, player.GetLeagueStats(League.LeagueID, League.Season));

            player.Approved = true;
            await OnPlayerRegistrationAccepted(player);
        }

        public async Task RejectRegistration(Player player, String reason)
        {
            Players.Remove(player);
            //TODO DBA: await _database.DeleteRegistration(player.SteamID, League);

            await OnPlayerRegistrationFailed(player, reason);
        }

        public async Task SetAutoAccept(Boolean autoAccept)
        {
            DiscordServer.AutoAccept = autoAccept;
            //TODO DBA: await _database.UpdateLeague(League);
        }

        public async Task SetSteamRegister(Boolean steamRegister)
        {
            DiscordServer.NeedSteamToRegister = steamRegister;
            //TODO DBA: await _database.UpdateLeague(League);
        }

        public async Task SetRegistrationRole(SocketRole role)
        {
            DiscordServer.RegistrationRole = role;
            //TODO DBA: await _database.UpdateLeague(League);
        }

        public async Task SetModChannel(SocketGuildChannel modChannel)
        {
            DiscordServer.ModeratorChannel = modChannel;
            //TODO DBA: await _database.UpdateLeague(League);
        }

        //TODO: Where did the Registration come from (which discord server or webApp)
        protected async Task OnPlayerRegistered(Player player)
        {
            EventHandler<RegistrationEventArgs> handler = PlayerRegistered;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerRegistrationAccepted(Player player)
        {
            EventHandler<RegistrationEventArgs> handler = PlayerRegistrationAccepted;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;
            args.ModChannel = DiscordServer.ModeratorChannel;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerRegistrationFailed(Player player, String message)
        {
            EventHandler<RegistrationEventArgs> handler = PlayerRegistrationAccepted;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;
            args.ModChannel = DiscordServer.ModeratorChannel;
            args.Message = message;

            handler?.Invoke(this, args);
        }
    }

    public class RegistrationEventArgs : EventArgs
    {
        public Player Player { get; set; } 
        public SocketGuildChannel ModChannel { get; set; }
        public String Message { get; set; }
    }
}