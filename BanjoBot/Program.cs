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
        private const String TOKEN = "XXXXXXXXXXXXXXXX";
        private DiscordSocketClient _bot;
        private List<SocketGuild> _connectedServers;
        private List<LeagueServer> _allServers;
        private DatabaseController _databaseController;
        private CommandService _commands;
        private DependencyMap _commandMap;
        private SocketServer _socketServer;

        [STAThread]
        public static void Main(string[] args)
        {
            //BasicConfigurator.Configure();
            //XmlConfigurator.Configure(new System.IO.FileInfo(args[0]));
            //log4net.Config.XmlConfigurator.Configure();
            // Handler for unhandled exceptions.
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
            _allServers = new List<LeagueServer>();
            _databaseController = new DatabaseController();
            

            // Initialise commands
            Console.WriteLine("Initialising commands...");
            CommandServiceConfig commandConfig = new CommandServiceConfig();
            commandConfig.CaseSensitiveCommands = false;
            _commands = new CommandService();
            _commandMap = new DependencyMap();
            await InitialiseCommands();

            await _bot.LoginAsync(TokenType.Bot, TOKEN);
            await _bot.ConnectAsync();
            await Task.Delay(-1);
        }

    

        private async Task LoadServerInformation(SocketGuild server)
        {
            Console.WriteLine("Downloading server information...");        
            LeagueServer matchMakingServer = await _databaseController.GetServer(server);
            if (matchMakingServer != null)
            {
                _allServers.Add(matchMakingServer);
                await LoadPlayerBase(matchMakingServer);
                await LoadMatchHistory(matchMakingServer);
            }
            else
            {
                matchMakingServer = await CreateServer(server);
                _allServers.Add(matchMakingServer);
            }
            Console.WriteLine("Server is ready!");
            _socketServer = new SocketServer(_allServers);
        }

        private async Task LoadPlayerBase(LeagueServer server)
        {
            Console.Write("Load Playerbase...");
            List<Player> allPlayers = await _databaseController.GetPlayerBase(server);
            foreach (var player in allPlayers)
            {
                server.RegisteredPlayers.Add(player);
                foreach (var leagueController in server.LeagueController)
                {
                    foreach (var playerLeagueStat in player.PlayerStats)
                    {
                        if (playerLeagueStat.LeagueID == leagueController.League.LeagueID)
                        {
                            leagueController.League.RegisteredPlayers.Add(player);
                        }
                    }
                }
            }
            Console.WriteLine("done!");
            Console.Write("Load Applicants...");
            foreach (var lc in server.LeagueController)
            {
                lc.League.Applicants = await _databaseController.GetApplicants(server, lc.League);
            }
            Console.WriteLine("done!");
        }

        private async Task LoadMatchHistory(LeagueServer server) {
            Console.Write("Load match history...");
            foreach (var lc in server.LeagueController)
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
                            Player player = server.GetPlayer(stats.DiscordID);
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

                        Player p = server.GetPlayer(stats.DiscordID);
                        if(p != null)
                            p.Matches.Add(stats);
                    }

                    if (lobby != null)
                    {
                        // Restore Lobby
                        lobby.MmrAdjustment = lobby.CalculateMmrAdjustment();
                    }
                }
            }
            Console.WriteLine("done!");
        }

        private async Task<LeagueServer> CreateServer(SocketGuild server)
        {
            return new LeagueServer(server);
        }

        private async Task ServerConnected(SocketGuild server)
        {
            if (!_connectedServers.Contains(server))
            {
                _connectedServers.Add(server);
                Console.WriteLine("Bot connected to a new server: " + server.Name + "(" + server.Id + ")");

                if(!IsServerInitialised(server))
                    await LoadServerInformation(server);    
            }

        }

        private async Task ServerDisconnected(SocketGuild socketGuild)
        {
            _connectedServers.Remove(socketGuild);
        }

        private bool IsServerInitialised(SocketGuild server) {
            foreach (LeagueServer myServer in _allServers) {
                if (myServer.DiscordServer.Id == server.Id)
                    return true;
            }

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
            _commandMap.Add(_allServers);
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
