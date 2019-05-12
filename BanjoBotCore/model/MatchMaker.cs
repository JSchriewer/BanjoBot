using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBotCore {
    public static class MatchMaker
    {
        public const int BASE_MMR = 25;
        
        public async static Task<int> CalculateMmrAdjustment(List<Player> winner, List<Player> looser, int leagueID, int season)
        {
            double mmrDifference = await GetTeamMMR(winner,leagueID,season) - await GetTeamMMR(looser,leagueID,season);

            return await mmrCurve(mmrDifference);
        }

        public async static Task<int> mmrCurve(double x)
        {
            double approaches = 10;
            double approachRate = Math.Atan(-x*(1/350.0));
            double result = approachRate*approaches + BASE_MMR;
            return Convert.ToInt32(Math.Round(result));
        }

        public async static Task<double> GetTeamMMR(List<Player> team, int leagueID, int season)
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
