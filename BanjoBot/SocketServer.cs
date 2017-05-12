using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace BanjoBot
{
    class SocketServer
    {
        private const string AUTH_KEY = "a2g9xCvASDh321oc9DVe";
        private const int SERVER_PORT = 3637;
        private static List<Socket> _clientSockets = new List<Socket>();
        private static Socket _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static byte[] _buffer = new byte[1024];
        private List<LeagueServer> _leagueServers;

        const string getinfo = @"{ 
                    'AUTH_KEY' : 'a2g9xCvASDh321oc9DVe', 
                    'Event' : 'get_match_info', 
                    'SteamIDs': [
                        76561197962516578,
                        76561197962516578,
                        76561197962516578,
                        76561197962516578,
                        76561197962516578,
                        76561197962516578,
                        76561197962516578,
                        76561197962516578,
                        76561197962516578,
                        76561197962516578
                    ]  
                }";

        const string match_result =
            @"{ 
                    'AUTH_KEY' : 'a2g9xCvASDh321oc9DVe', 
                    'Event' : 'match_result', 
                    'MatchResult': {
                        'MatchID' : '24', 
                        'LeagueID' : '23',
                        'SteamMatchID' : '156756167894',
                        'Season' : '1',
                        'Winner' : '2',
                        'Duration' : '1800',
                        'PlayerMatchStats': [
                            {
                                'SteamID':'12353453',
                                'Goals':'4',
                                'Assist':'2',
                                'Steals':'5',
                                'Turnovers':'5',
                                'StealTurnDif':'0',
                                'Pickups':'12',
                                'Passes':'15',
                                'PassesReceived':'3',
                                'SaveRate':'0.25',
                                'Points':'112',
                                'PossessionTime':'120',
                                'TimeAsGoalie':'100',
                                'Team':'2',
                            },  
                            {
                                'SteamID':'999999',
                                'Goals':'4',
                                'Assist':'2',
                                'Steals':'5',
                                'Turnovers':'5',
                                'StealTurnDif':'0',
                                'Pickups':'12',
                                'Passes':'15',
                                'PassesReceived':'3',
                                'SaveRate':'0.25',
                                'Points':'112',
                                'PossessionTime':'120',
                                'TimeAsGoalie':'100',
                                'Team':'2',
                            }
                        ]
                    }
                }";

        public SocketServer(List<LeagueServer> servers)
        {
            _leagueServers = servers;
            SetupServer();

            //ProcessMessage(getinfo);
        }

        private void SetupServer()
        {
            Console.WriteLine("Settings up server...");
            _serverSocket.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT));
            _serverSocket.Listen(16);
            _serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);

        }

        private void AcceptCallback(IAsyncResult AR)
        {
            Socket socket = _serverSocket.EndAccept(AR);
            _clientSockets.Add(socket);
            socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
            _serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), null);
        }

        private void ReceiveCallback(IAsyncResult AR)
        {
            Socket socket = (Socket) AR.AsyncState;
            int received = socket.EndReceive(AR);
            byte[] dataBuf = new byte[received];
            Array.Copy(_buffer, dataBuf, received);
            string message = Encoding.ASCII.GetString(dataBuf);
            Console.WriteLine("Message received: " + message);

            ProcessMessage(message, socket);
        }

        private void SendCallback(IAsyncResult AR)
        {
            Socket socket = (Socket) AR.AsyncState;
            socket.EndSend(AR);
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        private void ProcessMessage(string jsonString, Socket socket)
        {
            try
            {
                JObject json = JObject.Parse(jsonString);

                if (!json["AUTH_KEY"].ToString().Equals(AUTH_KEY))
                {
                    Console.WriteLine("Authorization failed");
                    return;
                }

                string eventtype = json["Event"].ToString();
                string responseString = "";
                if (eventtype.Equals("match_result"))
                {
                    CreateMatchResult(json);
                }
                else if (eventtype.Equals("get_match_info"))
                {
                    responseString = GetMatchInfo(json);
                }

                if (!responseString.Equals(""))
                {
                    byte[] data = Encoding.ASCII.GetBytes(responseString);
                    socket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendCallback), socket);
                }
            }
            catch (Exception e)
            {

                Console.WriteLine(e.Message);
                return;
            }
        }

        private string GetMatchInfo(JObject json)
        {
            try
            {
                
                JObject jObject = JObject.Parse(json.ToString());
                JToken jToken = jObject.GetValue("SteamIDs");
                ulong[] steamIDs = jToken.Values<ulong>().ToArray();
                List<Player> players = new List<Player>();
                foreach (var server in _leagueServers) {
                    for (int i = 0; i < steamIDs.Length; i++)
                    {
                        if (players.Count == 8)
                        {
                            break;    
                        }

                        Player result = server.RegisteredPlayers.Find(x => x.SteamID == steamIDs[i]);
                        if (result != null)
                        {
                            players.Add(result);
                        }
                    }

                }

                if (players.Count < 8)
                {
                    // not a league match
                    return "";
                }

                //TODO: check if started
                int matchID = Int32.MinValue;
                if (players[0].CurrentGame != null)
                {
                    matchID = players[0].CurrentGame.MatchID;
                    //leagueID = players[0].CurrentGame.League.LeagueID;
                }
                else
                {
                    // not a league match

                    //REMOVE ME
                    var response = new {
                        LeagueID = 23,
                        LeagueName = "EU-BBL",
                        Season = 1,
                        MatchID = 26,
                        Players = players.Select(player => new
                        {
                            SteamID = player.SteamID,
                            Team = Teams.Red,//player.CurrentGame.BlueWinCalls.Contains(player) ? Teams.Blue : Teams.Red,
                            MatchesCount = player.GetLeagueStat(23,1).MatchCount,
                            Wins = player.GetLeagueStat(23,1).Wins,
                            Losses = player.GetLeagueStat(23,1).Losses,
                            MMR = player.GetLeagueStat(23,1).MMR,
                            Streak = player.GetLeagueStat(23,1).Streak
                           

                        })};
                    Console.WriteLine(JsonConvert.SerializeObject(response));
                    return JsonConvert.SerializeObject(response);
                }

                bool check = true;
                foreach (var player in players)
                {
                    if (player != null && player.CurrentGame.MatchID != matchID)
                    {
                        check = false;
                    }
                }

                if (!check)
                {
                    // not a league match
                    return "";
                }
                else
                {
                    // remove constants to match details
                    var response = new {
                        LeagueID = 23,
                        LeagueName = "EU-BBL",
                        Season = 1,
                        MatchID = 26,
                        Players = players.Select(player => new {
                            SteamID = player.SteamID,
                            Team = Teams.Red,//player.CurrentGame.BlueWinCalls.Contains(player) ? Teams.Blue : Teams.Red,
                            MatchesCount = player.GetLeagueStat(23, 1).MatchCount,
                            Wins = player.GetLeagueStat(23, 1).Wins,
                            Losses = player.GetLeagueStat(23, 1).Losses,
                            MMR = player.GetLeagueStat(23, 1).MMR,
                            Streak = player.GetLeagueStat(23, 1).Streak
                          
                        })
                    };
                    Console.WriteLine(JsonConvert.SerializeObject(response));
                    return JsonConvert.SerializeObject(response);
                }

            }
            catch (Exception e) {

                Console.WriteLine(e.Message);
                return "Error";
            }
        }

        private string CreateMatchResult(JObject json)
        {
            try
            {
                Console.WriteLine(json["MatchResult"].ToString());
                MatchResult match = JsonConvert.DeserializeObject<MatchResult>(json["MatchResult"].ToString());
                LeagueController lc = null;
                foreach (var leagueServer in _leagueServers)
                {
                    lc = leagueServer.GetLeagueController(match.LeagueID);
                }

                if (lc != null)
                {
                    Task.Run(async () => {await lc.CloseGameByEvent(match);});

                    var response = new {
                        LeagueID = match.LeagueID,
                        MatchID = match.MatchID,
                        Players = match.PlayerMatchStats.Select(player => new {
                            SteamID = player.SteamID,
                            MMR = player.MmrAdjustment,
                            Streak = player.StreakBonus
                        })
                    };
                    Console.WriteLine(JsonConvert.SerializeObject(response));
                    return JsonConvert.SerializeObject(response);
                }
         
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return "error";
            }

            return "error";
        }

    }
}