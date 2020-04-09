using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BanjoBotCore.Model
{
    public class League
    {
        public LeagueDiscordConfig LeagueDiscordConfig { get; set; }
        public int LeagueID { get; set; }
        public string Name { get; set; } = "";
        public List<Match> Matches { get; set; } = new List<Match>();
        public int Season { get; set; }

        public League(int id, string name, int season)
        {
            LeagueID = id;
            Name = name;
            Season = season;
        }

        public bool HasDiscord()
        {
            if (LeagueDiscordConfig != null)
            {
                return true;
            }

            return false;
        }
    }
}