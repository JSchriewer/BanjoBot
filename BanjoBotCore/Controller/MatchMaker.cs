using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBotCore.Model
{
    public static class MatchMaker
    {
        public const int BASE_MMR = 25;
        
        public static int CalculateMmrAdjustment(int team1MMR, int team2MMR, int leagueID, int season)
        {
            double mmrDifference = team1MMR - team2MMR;
            return mmrCurve(mmrDifference);
        }

        private static int mmrCurve(double x)
        {
            double approaches = 10;
            double approachRate = Math.Atan(-x * (1 / 350.0));
            double result = approachRate * approaches + BASE_MMR;
            return Convert.ToInt32(Math.Round(result));
        }


        public static Tuple<List<Player>, List<Player>> BalanceTeams(List<Player> players, League league)
        {
            List<Player> team1 = new List<Player>();
            List<Player> team2 = new List<Player>();

            var numPlayers = players.Count;
            var mmrs = new List<int>();
            var subsets = TryCombinations(numPlayers);
            var storedTeams = new List<int>();
            var bestMmrDiff = double.PositiveInfinity;

            foreach (var s in subsets)
            {
                var mmr1 = 0;
                var mmr2 = 0;

                for (int i = 0; i < numPlayers; i++)
                {
                    if (s[i] == 1)
                        mmr1 += players[i].GetLeagueStats(league.LeagueID, league.Season).MMR;
                    else
                        mmr2 += players[i].GetLeagueStats(league.LeagueID, league.Season).MMR;
                }

                var difference = Math.Abs(mmr1 - mmr2);

                if (difference < bestMmrDiff)
                {
                    bestMmrDiff = difference;
                    storedTeams = s;
                }
            }
            

            for (int i = 0; i < numPlayers; i++)
            {
                if (storedTeams[i] == 1)
                    team1.Add(players[i]);
                else
                    team2.Add(players[i]);
            }

            return new Tuple<List<Player>, List<Player>>(team1, team2);
        }
        private static List<List<int>> TryCombinations(int numPlayers)
        {
            var output = new List<List<int>>();
            output.Add(new List<int>());
            output.Add(new List<int>());
            output[0].Add(1);
            output[1].Add(0);

            for (int i = 1; i < numPlayers; i++)
            {
                var count = output.Count;
                for (int oIndex = 0; oIndex < count; oIndex++)
                {
                    var o = output[oIndex];
                    var copy = o.Select(v => v).ToList();
                    o.Add(0);
                    copy.Add(1);
                    output.Add(copy);
                }
            }

            var freshOutput = new List<List<int>>();

            foreach (var o in output)
            {
                var sum = o.Sum();
                if (sum < numPlayers / 2 || sum > (numPlayers / 2) + 1)
                    continue;
                freshOutput.Add(o);
            }

            return freshOutput;
        }
    }
}
