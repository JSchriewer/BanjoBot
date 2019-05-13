using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using log4net.Config;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Microsoft.Extensions.Configuration;
using BanjoBotCore.persistence;
using BanjoBotCore.Controller;
using BanjoBotCore.Controller;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace BanjoBotCore
{
    //TODO: Fix namespaces BanjoBot / BanjoBotCore
    public class Program
    {
        private DiscordSocketClient _client;
        private IConfiguration _config;
        private static ILog _log;
        private List<SocketGuild> _connectedServers;
        private List<SocketGuild> _initialisedServers;
        private LeagueCoordinator _leagueCoordinator;
        private CommandController _commandController;
        private DatabaseController _databaseController;
        private CommandHandler _handler;
        //private SocketServer _socketServer;

        [STAThread]
        public static void Main(string[] args)
        {
            _log = LogManager.GetLogger(typeof(Program));
            AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            new Program().Run().GetAwaiter().GetResult();
        }

        public async Task Run()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
#if RELEASE 
                .AddJsonFile("appsettings.json", true, true);
#else
                .AddJsonFile("appsettings.dev.json",true,true);
#endif
            _config = builder.Build();

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug
            });

            _client.Log += Log;
            _client.GuildAvailable += ServerConnected;
            _client.GuildUnavailable += ServerDisconnected;
            _client.MessageReceived += BotOnMessageReceived;

            _log.Info("Initialising...");
            await Initialise();

            _log.Info("Loading data from database...");
            await LoadLeagueInformation();
            await LoadPlayerBase();
            await LoadMatchHistory();
    
            String token = _config.GetValue<String>("Token:Discord");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private async Task Initialise()
        {
            _connectedServers = new List<SocketGuild>();
            _initialisedServers = new List<SocketGuild>();
            _leagueCoordinator = LeagueCoordinator.Instance;
            _commandController = new CommandController();
            _databaseController = new DatabaseController();
            //_socketServer = new SocketServer(_leagueCoordinator, _databaseController);

            var serviceProvider = ConfigureServices();
            _handler = new CommandHandler(serviceProvider);

            await _handler.ConfigureAsync();
        }

        //private IServiceProvider ConfigureServices()
        //{
        //    var services = new ServiceCollection();
        //    services.AddSingleton(_client);
        //    services.AddSingleton(new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = false }));
        //    services.AddSingleton<IConfiguration>(_config);
        //    services.AddSingleton(_databaseController);
        //    ////services.AddSingleton(_leagueCoordinator);

        //    ////var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        //    //// Autowire and create these dependencies now
        //    ////provider.GetService<LogAdaptor>();
        //    ////provider.GetService<TagService>();
        //    ////provider.GetService<GitHubService>();
        //    ////return services.BuildServiceProvider(); ;
        //}

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(
                    new CommandService(new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = false }))
                .AddSingleton(_databaseController)
                .AddSingleton(_commandController) //Should not be a singleton service, multiple instance are fine
                .AddSingleton<IConfiguration>(_config);
            var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
            // Autowire and create these dependencies now
            //provider.GetService<LogAdaptor>();
            //provider.GetService<TagService>();
            //provider.GetService<GitHubService>();
            return provider;
        }

        private async Task LoadLeagueInformation()
        {

            _log.Info("Loading league information...");

            List<League> leagues = await _databaseController.GetLeagues();
            await _leagueCoordinator.AddLeague(leagues,_commandController);
        }

        private async Task LoadPlayerBase()
        {
            _log.Info("Loading playerbase...");
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

            _log.Info("Loading Applicants...");
            foreach (var lc in _leagueCoordinator.LeagueControllers)
            {
                lc.League.Applicants = await _databaseController.GetApplicants(_leagueCoordinator, lc.League);
            }
        }

        private async Task LoadMatchHistory()
        {
            _log.Info("Loading match history...");
            foreach (var lc in _leagueCoordinator.LeagueControllers)
            {
                List<MatchResult> matches = await _databaseController.GetMatchHistory(lc.League.LeagueID);
                lc.League.Matches = matches;
                foreach (var matchResult in matches)
                {
                    matchResult.League = lc.League;
                    Lobby lobby = null;
                    if (matchResult.Winner == Teams.None)
                    {
                        // Restore Lobby
                        lobby = new Lobby(lc.League);
                        lobby.MatchID = matchResult.MatchID;
                        lobby.League = lc.League;
                        lobby.HasStarted = true;
                        lc.GamesInProgress.Add(lobby);
                    }

                    foreach (var stats in matchResult.PlayerMatchStats)
                    {
                        if (lobby != null)
                        {
                            Player player = _leagueCoordinator.GetPlayerBySteamID(stats.SteamID);
                            stats.Player = player;

                            // Restore Lobby
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
                        if (p != null)
                            p.Matches.Add(matchResult);
                    }

                    //if (lobby != null)
                    //{
                    //    // Restore Lobby
                    //    lobby.MmrAdjustment = lobby.CalculateMmrAdjustment();
                    //}
                }
            }
        }

        private async Task ServerConnected(SocketGuild server)
        {
            if (!_connectedServers.Contains(server))
            {
                _connectedServers.Add(server);
                _log.Info("Bot connected to a new server: " + server.Name + "(" + server.Id + ")");

                if (!IsServerInitialised(server))
                    await UpdateDiscordInformation(server);
            }

        }

        private async Task UpdateDiscordInformation(SocketGuild server)
        {
            //TODO: Gets called on reconnect
            //What happens if you leave and reconnect to the server? with or without a bot restart?
            //server.GetUser != _client.GetUser
            _log.Info("Update discord information " + server.Name + "(" + server.Id + ")...");
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
                    if (user == null)
                    {
                        deletedDiscordAccounts.Add(player);
                    }
                    else
                    {
                        player.User = user;
                    }
                }

                foreach (var player in lc.League.Applicants)
                {
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

                foreach (Player player in deletedDiscordAccounts)
                {
                    lc.League.RegisteredPlayers.Remove(player);
                }
            }
            _initialisedServers.Add(server);
        }

        private async Task ServerDisconnected(SocketGuild socketGuild)
        {
            _connectedServers.Remove(socketGuild);

            //Workaround for https://github.com/RogueException/Discord.Net/issues/960
            //TODO: Wait for a Timeout
            Environment.Exit(1);

        }

        private bool IsServerInitialised(SocketGuild server)
        {
            if (_initialisedServers.Contains(server))
                return true;

            return false;
        }

        private async Task BotOnMessageReceived(SocketMessage socketMessage)
        {
            foreach (var socketMessageMentionedUser in socketMessage.MentionedUsers)
            {
                if (socketMessageMentionedUser.Id == _client.CurrentUser.Id)
                    await socketMessage.Channel.SendMessageAsync("Fuck you");
            }

        }

        private async Task OnNewMember(SocketMessage socketMessage)
        {
            foreach (var socketMessageMentionedUser in socketMessage.MentionedUsers)
            {
                if (socketMessageMentionedUser.Id == _client.CurrentUser.Id)
                    await socketMessage.Channel.SendMessageAsync("Fuck you");
            }

        }

        private Task Log(LogMessage arg)
        {
            _log.Debug(arg.Message);
            return Task.CompletedTask;
        }

        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = default(Exception);
            ex = (Exception)e.ExceptionObject;
            _log.Error(ex.Message + "\n" + ex.StackTrace);
        }
    }
}
