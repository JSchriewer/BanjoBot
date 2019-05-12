using System.Collections.Generic;
using Discord;
using Discord.API;
using Discord.API.Gateway;
using Discord.WebSocket;

namespace BanjoBotCore {
    public class League
    {
        public DiscordInformation DiscordInformation { get; set; } = null;
        public int LeagueID { get; set; }
        public string Name { get; set; } = "";
        public List<Player> RegisteredPlayers { get; set; }
        public List<Player> Applicants { get; set; }
        public List<MatchResult> Matches { get; set; }
        public int Season { get; set; }
        public int GameCounter { get; set; }

        public League(int id, string name ,int season, int gameCounter = 0) {
            LeagueID = id;
            Name = name;
            Season = season;
            GameCounter = gameCounter;
            RegisteredPlayers = new List<Player>();
            Applicants = new List<Player>();
            Matches = new List<MatchResult>();
        }

        public Player GetPlayerByDiscordID(ulong id)
        {
            foreach (Player player in RegisteredPlayers)
            {
                if (player.User.Id == id) {
                    return player;
                }
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

        public bool HasDiscord() {
            if (DiscordInformation != null && DiscordInformation.DiscordServer != null)
            {
                return true;
            }

            return false;
        }
    }
}
