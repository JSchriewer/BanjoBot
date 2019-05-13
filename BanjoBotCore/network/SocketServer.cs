using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;

namespace BanjoBotCore
{
    class SocketServer
    {
        private const string AUTH_KEY = "a2g9xCvASDh321oc9DVe";
        private const int SERVER_PORT = 3637;
        private static List<Socket> _clientSockets = new List<Socket>();
        private static Socket _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static byte[] _buffer = new byte[4096];
        private LeagueCoordinator _leagueCoordinator;
        private DatabaseController _db;

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
                        'Winner' : '1',
                        'Duration' : '1800',
                        'PlayerMatchStats': [
                            {
                                'HeroID' : '12',
                                'SteamID':'765610',
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
                                'Team':'0',
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
                                'Team':'1',
                            }
                        ]
                    }
                }";

        public SocketServer(LeagueCoordinator leagueCoordinator, DatabaseController db)
        {
            _leagueCoordinator = leagueCoordinator;
            _db = db;
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

                if (steamIDs.Length != 8)
                {
                    //TODO return "";
                }

                for (int i = 0; i < steamIDs.Length; i++)
                {
                    Player result = _leagueCoordinator.GetPlayerBySteamID(steamIDs[i]);
                    if (result != null)
                    {
                        players.Add(result);
                    }
                    else
                    {
                        //Create new player
                        Player newPlayer = new Player(steamIDs[i]);
                        AsyncContext.Run(() =>  _leagueCoordinator.GetPublicLeague().RegisterPlayer(newPlayer)); 
                        players.Add(newPlayer);
                    }
                }

                Lobby lobby = _leagueCoordinator.FindLobby(players);
                if(lobby == null) {
                    // not a league match
                    LeagueController lc = _leagueCoordinator.GetPublicLeague();
                    League pubLeague = lc.League;

                    //Register player to public league
                    foreach (var player in players) {
                        if (!pubLeague.RegisteredPlayers.Contains(player))
                        {
                            AsyncContext.Run(() => lc.RegisterPlayer(player));
                        }
                    }

                    var response = new {
                        LeagueID = pubLeague.LeagueID,
                        LeagueName = pubLeague.Name,
                        Season = pubLeague.Season,
                        MatchID = -1,
                        Players = players.Select(player => new
                        {
                            SteamID = player.SteamID,
                            Team = Teams.None,
                            MatchesCount = player.GetLeagueStats(pubLeague.LeagueID,pubLeague.Season).MatchCount,
                            Wins = player.GetLeagueStats(pubLeague.LeagueID, pubLeague.Season).Wins,
                            Losses = player.GetLeagueStats(pubLeague.LeagueID, pubLeague.Season).Losses,
                            MMR = player.GetLeagueStats(pubLeague.LeagueID, pubLeague.Season).MMR,
                            Streak = player.GetLeagueStats(pubLeague.LeagueID, pubLeague.Season).Streak
                        })
                    };
                    return JsonConvert.SerializeObject(response);
                }
                else
                {
                    League league = lobby.League;
                    var response = new {
                        LeagueID = league.LeagueID,
                        LeagueName = league.Name,
                        Season = league.Season,
                        MatchID = lobby.MatchID,
                        Players = players.Select(player => new {
                            SteamID = player.SteamID,
                            Team = lobby.BlueWinCalls.Contains(player) ? Teams.Blue : Teams.Red,
                            MatchesCount = player.GetLeagueStats(league.LeagueID, league.Season).MatchCount,
                            Wins = player.GetLeagueStats(league.LeagueID, league.Season).Wins,
                            Losses = player.GetLeagueStats(league.LeagueID, league.Season).Losses,
                            MMR = player.GetLeagueStats(league.LeagueID, league.Season).MMR,
                            Streak = player.GetLeagueStats(league.LeagueID, league.Season).Streak
                          
                        })
                    };
                    return JsonConvert.SerializeObject(response);
                }
            }
            catch (Exception e) {
                Console.WriteLine(e.Message);
                return "";
            }

           
        }

        private string CreateMatchResult(JObject json)
        {
            try
            {
                Console.WriteLine(json["MatchResult"].ToString());
                MatchResult match = JsonConvert.DeserializeObject<MatchResult>(json["MatchResult"].ToString());
                LeagueController lc = null;

                if (match.PlayerMatchStats.Count != 8)
                {
                    //TODO: return "";
                }

                lc = AsyncContext.Run(() => _leagueCoordinator.GetLeagueController(match.LeagueID));
                if (lc != null)
                {
                    Task.Run(async () => {await lc.CloseLobbyByEvent(match);});

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
                return "";
            }

            return "";
        }

    }
}