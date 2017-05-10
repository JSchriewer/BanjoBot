using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using log4net.Config;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace BanjoBot
{
    public class Program
    {
        //TODO: Crash recovery http://stackoverflow.com/questions/5302585/crash-recovery-in-application
        //TODO: cant connect, retry
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        private const String TOKEN = "MjU1NDQ1NzU4Mjk4NDIzMjk3.C9Jh4w.I4-UxgqN7m_nzN3qYpIGP-IO9WE";
        private DiscordSocketClient _bot;
        private List<SocketGuild> _connectedServers;
        private List<SocketGuild> _initialisedServers;
        private LeagueCoordinator _leagueCoordinator;
        private DatabaseController _databaseController;
        private CommandService _commands;
        private DependencyMap _commandMap;
        private SocketServer _socketServer;

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            new Program().Run().GetAwaiter().GetResult();
        }

        public async Task Run() 
        {
            _bot = new DiscordSocketClient();
            _bot.GuildAvailable += ServerConnected;
            _bot.GuildUnavailable += ServerDisconnected;
            _bot.MessageReceived += BotOnMessageReceived;
            _connectedServers = new List<SocketGuild>();
            _initialisedServers = new List<SocketGuild>();
            _leagueCoordinator = LeagueCoordinator.Instance;
            _databaseController = new DatabaseController();

            // Initialise commands
            Console.WriteLine("Initialising commands...");
            CommandServiceConfig commandConfig = new CommandServiceConfig();
            commandConfig.CaseSensitiveCommands = false;
            _commands = new CommandService();
            _commandMap = new DependencyMap();
            await InitialiseCommands();
            await LoadLeagueInformation();
            await LoadPlayerBase();
            await LoadMatchHistory();
            _socketServer = new SocketServer(_leagueCoordinator, _databaseController);

            await _bot.LoginAsync(TokenType.Bot, TOKEN);
            await _bot.ConnectAsync();
            await Task.Delay(-1);
        }

    

        private async Task LoadLeagueInformation()
        {
     
            Console.Write("Downloading league information...");

            List<League> leagues = await _databaseController.GetLeagues();
            _leagueCoordinator.AddLeague(leagues);
            Console.WriteLine("done!");
        }

        private async Task LoadPlayerBase()
        {
            Console.Write("Load Playerbase...");
            List<Player> allPlayers = await _databaseController.GetPlayerBase(_leagueCoordinator);
            foreach (Player player in allPlayers)
            {
                foreach (var playerLeagueStat in player.PlayerStats)
                {
                    LeagueController lc = _leagueCoordinator.GetLeagueController(playerLeagueStat.LeagueID);
                    if (!lc.League.RegisteredPlayers.Contains(player))
                    {
                        lc.League.RegisteredPlayers.Add(player);
                    }
                }
                
            }
            Console.WriteLine("done!");

            Console.Write("Load Applicants...");
            foreach (var lc in _leagueCoordinator.LeagueControllers)
            {
                lc.League.Applicants = await _databaseController.GetApplicants(_leagueCoordinator, lc.League);
            }
            Console.WriteLine("done!");
        }

        private async Task LoadMatchHistory() {
            Console.Write("Load match history...");
            foreach (var lc in _leagueCoordinator.LeagueControllers)
            {
                List<MatchResult> matches = await _databaseController.GetMatchHistory(lc.League.LeagueID);
                lc.League.Matches = matches;
                foreach (var matchResult in matches)
                {
                    Lobby lobby = null;
                    if (matchResult.Winner == Teams.None)
                    {
                        // Restore Lobby
                        lobby = new Lobby(lc.League);
                        lobby.MatchID = matchResult.MatchID;
                        lobby.League = lc.League;
                        lobby.GameNumber = lc.League.GameCounter; //TODO: Can be wrong, persistent lobby when
                        lobby.HasStarted = true;
                        lc.RunningGames.Add(lobby);
                    }
                    
                    foreach (var stats in matchResult.PlayerMatchStats)
                    {
                        if (lobby != null)
                        {
                            // Restore Lobby
                            Player player = _leagueCoordinator.GetPlayerBySteamID(stats.SteamID);
                            lobby.Host = player;
                            player.CurrentGame = lobby;
                            lobby.WaitingList.Add(player);
                            if (stats.Team == Teams.Blue)
                            {
                                lobby.BlueList.Add(player);
                            }
                            else
                            {
                                lobby.RedList.Add(player);
                            }
                        }

                        Player p = _leagueCoordinator.GetPlayerBySteamID(stats.SteamID);
                        if(p != null)
                            p.Matches.Add(stats);
                    }

                    //if (lobby != null)
                    //{
                    //    // Restore Lobby
                    //    lobby.MmrAdjustment = lobby.CalculateMmrAdjustment();
                    //}
                }
            }
            Console.WriteLine("done!");
        }

        private async Task ServerConnected(SocketGuild server)
        {
            if (!_connectedServers.Contains(server))
            {
                _connectedServers.Add(server);
                Console.WriteLine("Bot connected to a new server: " + server.Name + "(" + server.Id + ")");

                if(!IsServerInitialised(server))
                    await UpdateDiscordInformation(server);       
            }

        }

        private async Task UpdateDiscordInformation(SocketGuild server)
        {
            Console.Write("Update discord information " + server.Name + "(" + server.Id + ")...");
            foreach (var lc in _leagueCoordinator.GetLeagueControllersByServer(server))
            {
                if (lc.League.DiscordInformation != null)
                {
                    if (lc.League.DiscordInformation.DiscordServerId == server.Id)
                    {
                        lc.League.DiscordInformation.DiscordServer = server;
                    }   
                }

                foreach (var player in lc.League.RegisteredPlayers)
                {
                    player.User = server.GetUser(player.discordID);
                }

                foreach (var player in lc.League.Applicants) {
                    player.User = server.GetUser(player.discordID);
                }
            }
            _initialisedServers.Add(server);
            Console.WriteLine("done!");
        }

        private async Task ServerDisconnected(SocketGuild socketGuild)
        {
            _connectedServers.Remove(socketGuild);
        }

        private bool IsServerInitialised(SocketGuild server)
        {
            if (_initialisedServers.Contains(server))
                return true;

            return false;
        }

        private async Task BotOnMessageReceived(SocketMessage socketMessage) {
            foreach (var socketMessageMentionedUser in socketMessage.MentionedUsers)
            {
                if(socketMessageMentionedUser.Id == _bot.CurrentUser.Id)
                    await socketMessage.Channel.SendMessageAsync("Fuck you");
            }

        }

        private async Task OnNewMember(SocketMessage socketMessage) {
            foreach (var socketMessageMentionedUser in socketMessage.MentionedUsers) {
                if (socketMessageMentionedUser.Id == _bot.CurrentUser.Id)
                    await socketMessage.Channel.SendMessageAsync("Fuck you");
            }

        }

        /// <summary>
        /// Creates the commands.
        /// </summary>
        private async Task InitialiseCommands()
        {
            _commandMap.Add(_bot);
            _commandMap.Add(_databaseController);
            _commandMap.Add(_commands);
        
            _bot.MessageReceived += HandleCommand;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix('!', ref argPos))) return;
            // Create a Command Context
            var context = new CommandContext(_bot, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed succesfully)
            var result = await _commands.ExecuteAsync(context, argPos, _commandMap);
            if (!result.IsSuccess)
                await message.Channel.SendMessageAsync(result.ErrorReason);
        }

        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e) {
            Exception ex = default(Exception);
            ex = (Exception)e.ExceptionObject;
            log.Error(ex.Message + "\n" + ex.StackTrace);
            Console.WriteLine("Fatal Error" + ex.Message + "\n Stacktrace:\n" + ex.StackTrace);
        }
    }
}
