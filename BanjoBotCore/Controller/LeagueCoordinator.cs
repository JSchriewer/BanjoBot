using System;
using System.Collections.Generic;
using System.Linq;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace BanjoBot {
    public class LeagueCoordinator
    {
        private static readonly int PUBLIC_LEAGUE_ID = 25;
        ////private readonly IServiceProvider _services;
        private static readonly LeagueCoordinator INSTANCE = new LeagueCoordinator();
        public List<LeagueController> LeagueControllers { get; set; }
 
        private LeagueCoordinator()
        {
            ////_services = serviceProvider;
            LeagueControllers = new List<LeagueController>();
        }
  
        public static LeagueCoordinator Instance
        {
            get { return INSTANCE; }
        }

        public void AddLeague(League league)
        {
            ////DatabaseController database = _services.GetService<DatabaseController>();
            ////LeagueControllers.Add(new LeagueController(league, database));
            LeagueControllers.Add(new LeagueController(league));
        }

        public void AddLeague(List<League> leagues) {
            foreach (var league in leagues)
            {
                AddLeague(league);
            }
        }

        public void DeleteLeague(LeagueController league) {
            LeagueControllers.Remove(league);
        }

        public LeagueController GetLeagueController(SocketGuildChannel channel)
        {
            if (channel == null)
            {
                System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
                Console.WriteLine("Error channel == null\n" + t);
            }

            foreach (LeagueController leagueController in LeagueControllers)
            {
                if (leagueController.League.DiscordInformation != null && leagueController.League.DiscordInformation.DiscordServer != null)
                {
                    if(leagueController.League.DiscordInformation.Channel == channel)
                        return leagueController;
                }
            }
            return null;
        }

        public List<LeagueController> GetLeagueControllersByServer(SocketGuild guild)
        {
            List<LeagueController> result = new List<LeagueController>();
            foreach (LeagueController leagueController in LeagueControllers) {
                if (leagueController.League.DiscordInformation != null && leagueController.League.DiscordInformation.DiscordServerId == guild.Id) {
                    result.Add(leagueController);
                }
            }
            return result;
        }

        public LeagueController GetLeagueController(int LeagueID) {
            foreach (LeagueController leagueController in LeagueControllers) {
                if (leagueController.League.LeagueID == LeagueID) {
                    return leagueController;
                }
            }

            return null;
        }

        public Player GetPlayerByDiscordID(ulong userID) {
            foreach (var lc in LeagueControllers)
            {
                foreach (var regplayer in lc.League.RegisteredPlayers)
                {
                    if (regplayer.discordID == userID) {
                        return regplayer;
                    }
                }
            }
         
            return null;
        }


        public Player GetPlayerBySteamID(ulong steamID) {
            foreach (var lc in LeagueControllers) {
                foreach (var regplayer in lc.League.RegisteredPlayers) {
                    if (regplayer.SteamID == steamID) {
                        return regplayer;
                    }
                }
            }

            return null;
        }

        public Lobby FindLobby(List<Player> players)
        {
            if (players.Count != 8)
            {
                //TODO return null;
            }

            Lobby lobby  = players.First().CurrentGame;

            if (lobby == null)
            {
                return null;
            }

            foreach (var player in players)
            {
                Player result = lobby.WaitingList.Find(p => p.SteamID == player.SteamID);
                if (result == null)
                {
                    return null;
                }
            }

            return lobby;
        }

        public LeagueController GetPublicLeague()
        {
            foreach (var leagueController in LeagueControllers)
            {
                if (leagueController.League.LeagueID == PUBLIC_LEAGUE_ID)
                {
                    return leagueController;
                }
            }

            return null;
        }
    }
}
