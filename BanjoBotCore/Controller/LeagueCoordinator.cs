using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BanjoBotCore.Controller;
using BanjoBotCore.Model;
using Discord.WebSocket;
using log4net;
using Microsoft.Extensions.DependencyInjection;

namespace BanjoBotCore {

    //TODO: Catch Database Exceptions

    public class LeagueCoordinator
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(LeagueController));
        private static readonly int PUBLIC_LEAGUE_ID = 25;
        private static readonly LeagueCoordinator INSTANCE = new LeagueCoordinator();
        public List<LeagueController> LeagueControllers { get; set; }
        private DatabaseController _database;

        private LeagueCoordinator()
        {
            _database = new DatabaseController();
            LeagueControllers = new List<LeagueController>();
        }
  
        public static LeagueCoordinator Instance
        {
            get { return INSTANCE; }
        }

        public async Task CreateLeague(String name, DiscordInformation discordInfo)
        {
            log.Debug("Create new league (Name = " + name + " )");
            int leagueID = await _database.InsertLeague();
            League league = new League(leagueID, name, 1);
            league.DiscordInformation = discordInfo;
            await AddLeague(league);
            await _database.UpdateLeague(league);
        }

        public async Task DeleteLeague(LeagueController lc)
        {
            await _database.DeleteLeague(lc.League);
            LeagueControllers.Remove(lc);
        }

        public  async Task<LeagueController> AddLeague(League league)
        {
            LeagueController newLeague = new LeagueController(league);
            LeagueControllers.Add(newLeague);

            return newLeague;
        }

        public async Task AddLeague(League league, ILeagueEventListener listener)
        {
            LeagueController lc = await AddLeague(league);
            await lc.RegisterEventListener(listener);
        }

        public async Task AddLeague(List<League> leagues) {
            foreach (var league in leagues)
            {
                await AddLeague(league);
            }
        }

        public async Task AddLeague(List<League> leagues, ILeagueEventListener listener)
        {
            foreach (var league in leagues)
            {
                await AddLeague(league, listener);
            }
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
