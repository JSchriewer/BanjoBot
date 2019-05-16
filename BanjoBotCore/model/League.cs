using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.API;
using Discord.API.Gateway;
using Discord.WebSocket;

namespace BanjoBotCore.Model
{
    public class League
    {
        public DiscordInformation DiscordInformation { get; set; } = null;
        public int LeagueID { get; set; }
        public string Name { get; set; } = "";
        public List<Player> RegisteredPlayers { get; set; }
        public List<Player> Applicants { get; set; }
        public List<Match> Matches { get; set; }
        public int Season { get; set; }
        public int GameCounter { get; set; }

        public League(int id, string name ,int season, int gameCounter = 0) {
            LeagueID = id;
            Name = name;
            Season = season;
            GameCounter = gameCounter;
            RegisteredPlayers = new List<Player>();
            Applicants = new List<Player>();
            Matches = new List<Match>();
        }

        public Player GetPlayerByDiscordID(ulong id)
        {
            foreach (Player player in RegisteredPlayers)
            {
                if (player.discordID == id) {
                    return player;
                }
            }
            
            return null;
        }


        public Player GetApplicantByDiscordID(ulong id) {
            foreach (Player player in Applicants) {
                if (player.discordID == id)
                    return player;
            }
            return null;
        }

        public List<Player> GetLeaderBoard()
        {
            return (List<Player>)RegisteredPlayers.OrderBy(player => player.GetLeagueStats(LeagueID, Season).MMR).Reverse().ToList();
        }

        public List<Player> GetLeaderBoard(int season)
        {
            return (List<Player>)RegisteredPlayers.OrderBy(player => player.GetLeagueStats(LeagueID, season).MMR).Reverse().ToList();
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
