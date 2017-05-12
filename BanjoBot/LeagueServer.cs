using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.API;
using Discord.WebSocket;

namespace BanjoBot {
    public class LeagueServer
    {
        public SocketGuild DiscordServer { get; set; }
        public List<LeagueController> LeagueController { get; set; }
        public List<Player> RegisteredPlayers { get; set; }
        public SocketGuildChannel ModeratorChannel { get; set; } //TODO

        public LeagueServer(SocketGuild discordServer)
        {
            DiscordServer = discordServer;
            LeagueController = new List<LeagueController>();
        }

        public LeagueServer(SocketGuild discordServer, List<League> leagues)
        {
            DiscordServer = discordServer;
            LeagueController = new List<LeagueController>();
            RegisteredPlayers = new List<Player>();
            foreach (League league in leagues)
            {
                LeagueController.Add(new LeagueController(this,league));
            }
        }

        public void AddLeague(League league)
        {
            LeagueController.Add(new LeagueController(this,league));
        }

        public void DeleteLeague(LeagueController league) {
            LeagueController.Remove(league);
        }

        public LeagueController GetLeagueController(SocketGuildChannel channel)
        {
            // Global League
            if (LeagueController.Count == 1 && LeagueController.First().League.Channel == null)
            {
                return LeagueController.First();
            }

            // Get League by Channel
            foreach (LeagueController leagueController in LeagueController)
            {
                if (leagueController.League.Channel == channel)
                {
                    return leagueController;
                }
            }

            return null;
        }

        public LeagueController GetLeagueController(int LeagueID) {
            // Global League
            if (LeagueController.Count == 1 && LeagueController.First().League.Channel == null) {
                return LeagueController.First();
            }

            // Get League by Channel
            foreach (LeagueController leagueController in LeagueController) {
                if (leagueController.League.LeagueID == LeagueID) {
                    return leagueController;
                }
            }

            return null;
        }

        public bool IsGlobal()
        {
            if (LeagueController.Count == 1 && LeagueController.First().League.Channel == null)
                return true;
            else
                return false;

        }

        public bool IsLeagueChannel(SocketGuildChannel channel)
        {
            foreach (LeagueController leagueController in LeagueController) {
                if (leagueController.League.Channel == channel) {
                    return true;
                }
            }

            return false;
        }

        public Player GetPlayer(ulong userID) {

            foreach (var player in RegisteredPlayers) {
                if (player.User.Id == userID) {
                    return player;
                }
            }
            return null;
        }
    }
}
