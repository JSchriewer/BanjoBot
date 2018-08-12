using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BanjoBot.Controller;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using log4net.Config;
using Microsoft.Extensions.DependencyInjection;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace BanjoBot
{
    public class Program
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private const String TOKEN = "XXXXXXXXXXXXXX";
        private DiscordSocketClient _bot;
        private List<SocketGuild> _connectedServers;
        private List<SocketGuild> _initialisedServers;
        private LeagueCoordinator _leagueCoordinator;
        private DatabaseController _databaseController;
        private CommandHandler _handler;
        //private SocketServer _socketServer;

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            new Program().Run().GetAwaiter().GetResult();
        }

        public async Task Run()
        {
            _bot = new DiscordSocketClient(new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Debug
                });

            //_bot.Log += Log;
            _bot.Log += bot_Log;
            _bot.GuildAvailable += ServerConnected;
            _bot.GuildUnavailable += ServerDisconnected;
            _bot.MessageReceived += BotOnMessageReceived;
            _connectedServers = new List<SocketGuild>();
            _initialisedServers = new List<SocketGuild>();
            _leagueCoordinator = LeagueCoordinator.Instance;
            _databaseController = new DatabaseController();

            // Initialise commands
            Console.WriteLine("Initialising commands...");
            await InitialiseCommands();
            await LoadLeagueInformation();
            await LoadPlayerBase();
            await LoadMatchHistory();
            //_socketServer = new SocketServer(_leagueCoordinator, _databaseController);
            
            await _bot.LoginAsync(TokenType.Bot, TOKEN);
            await _bot.StartAsync();
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
            //TODO: Gets called on reconnect
            //What happens if you leave and reconnect to the server? with or without a bot restart?
            //server.GetUser != _bot.GetUser
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

                List<Player> deletedDiscordAccounts = new List<Player>();
                foreach (var player in lc.League.RegisteredPlayers)
                {
                    SocketGuildUser user = server.GetUser(player.discordID);
                    if(user == null)
                    {
                        deletedDiscordAccounts.Add(player);
                    }
                    else
                    {
                        player.User = user;
                    }              
                }

                foreach (var player in lc.League.Applicants) {
                    SocketGuildUser user = server.GetUser(player.discordID);
                    if (user == null)
                    {
                        deletedDiscordAccounts.Add(player);
                    }
                    else
                    {
                        player.User = user;
                    }
                }

                foreach(Player player in deletedDiscordAccounts)
                {
                    lc.League.RegisteredPlayers.Remove(player);
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
            var serviceProvider = ConfigureServices();
            _handler = new CommandHandler(serviceProvider);
            await _handler.ConfigureAsync();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection()
                .AddSingleton(_bot)
                .AddSingleton(
                    new CommandService(new CommandServiceConfig {CaseSensitiveCommands = false, ThrowOnError = false}))
                .AddSingleton(_databaseController);
            var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
            // Autowire and create these dependencies now
            //provider.GetService<LogAdaptor>();
            //provider.GetService<TagService>();
            //provider.GetService<GitHubService>();
            return provider;
        }

        private async Task bot_Log(LogMessage arg)
        {
            log.Debug(arg.Message);
        }

        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = default(Exception);
            ex = (Exception)e.ExceptionObject;
            log.Error(ex.Message + "\n" + ex.StackTrace);
            Console.WriteLine("[" + System.DateTime.Now + "]" + "Fatal Error " + ex.Message + "\n Stacktrace:\n" + ex.StackTrace);
        }
    }
}
