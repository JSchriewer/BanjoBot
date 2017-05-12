using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBot.model {
    public static class MatchMaker
    {
        public const int BASE_MMR = 25;
        
        public static int CalculateMmrAdjustment(List<Player> winner, List<Player> looser, int leagueID, int season)
        {
            double mmrDifference = 0;
            mmrDifference = GetTeamMMR(winner,leagueID,season) - GetTeamMMR(looser,leagueID,season);

            return mmrCurve(mmrDifference);
        }

        public static int mmrCurve(double x)
        {
            double approaches = 10;
            double approachRate = Math.Atan(-x*(1/350.0));
            double result = approachRate*approaches + BASE_MMR;
            return Convert.ToInt32(result);
        }

        public static int GetTeamMMR(List<Player> team, int leagueID, int season)
        {
            int averageMMR = 0;
            foreach (var player in team)
            {
                averageMMR += player.GetLeagueStat(leagueID, season).MMR;
            }
            //TODO: Remove me
            if (team.Count < 1)
            {
                averageMMR = 1000;
            }
            else
            {
                averageMMR = averageMMR / team.Count;
            }

            return averageMMR;
        }
    }
}
