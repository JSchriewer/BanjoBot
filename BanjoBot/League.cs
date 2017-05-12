using System.Collections.Generic;
using Discord;
using Discord.API;
using Discord.API.Gateway;
using Discord.WebSocket;

namespace BanjoBot {
    public class League {
        public int LeagueID { get; set; }
        public string Name { get; set; } = "";
        public List<Player> RegisteredPlayers { get; set; }
        public List<Player> Applicants { get; set; }
        public List<MatchResult> Matches { get; set; }
        public SocketGuildChannel Channel { get; set; }
        public SocketRole ModeratorRole { get; set;  }
        public SocketRole Role { get; set;  } 
        public int Season { get; set; }
        public int GameCounter { get; set; }
        public bool AutoAccept { get; set; } = true;
        public bool NeedSteamToRegister { get; set; } = true; 

        public League(int id, string name ,int season, SocketGuildChannel channel, SocketRole moderatorRole, int gameCounter = 0) {
            LeagueID = id;
            Name = name;
            Season = season;
            Channel = channel;
            ModeratorRole = moderatorRole;
            GameCounter = gameCounter;
            RegisteredPlayers = new List<Player>();
            Applicants = new List<Player>();
            Matches = new List<MatchResult>();
        }

        public Player GetPlayerByDiscordID(ulong id)
        {
            foreach (Player player in RegisteredPlayers)
            {
                if (player.User.Id == id)
                    return player;
            }
            return null;
        }


        public Player GetApplicantByDiscordID(ulong id) {
            foreach (Player player in Applicants) {
                if (player.User.Id == id)
                    return player;
            }
            return null;
        }
    }
}
