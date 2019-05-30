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
using BanjoBotCore.Controller;
using System.Threading;
using BanjoBotCore.Model;
using Discord.Rest;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace BanjoBotCore
{
    public class Program
    {
        private const int MillisecondsTimeout = 30000;
        private DiscordSocketClient _client;
        private IConfiguration _config;
        private static ILog _log;
        private List<SocketGuild> _connectedServers;
        private List<SocketGuild> _initialisedServers;
        private LeagueCoordinator _leagueCoordinator;
        private CommandController _commandController;
        private DatabaseController _databaseController;
        private CommandHandler _handler;
        private DiscordMessageDispatcher _messageDispatcher;
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
            await LoadLeagueInformation();
    
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
            _messageDispatcher = new DiscordMessageDispatcher();
            _commandController = new CommandController(_messageDispatcher);
            _databaseController = new DatabaseController();
            //_socketServer = new SocketServer(_leagueCoordinator, _databaseController);

            Thread t = new Thread(new ThreadStart(_messageDispatcher.Run));
            t.Start();

            var serviceProvider = ConfigureServices();
            _handler = new CommandHandler(serviceProvider);

            await _handler.ConfigureAsync();
        }

        private IServiceProvider ConfigureServices()
        {
            CommandServiceConfig commandConfig = new CommandServiceConfig{ CaseSensitiveCommands = false, ThrowOnError = false };
            //commandConfig.DefaultRunMode = RunMode.Async;
            CommandService commandService = new CommandService(commandConfig);
            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(commandService)                  
                .AddSingleton(_databaseController)
                .AddSingleton(_commandController) //Should not be a singleton service, multiple instance should be fine
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

        private async Task LoadPlayerBase(League league)
        {
            _log.Info($"Loading player base of {league.Name}({league.LeagueID})");
            LeagueController lc = _leagueCoordinator.GetLeagueController(league.LeagueID);
            List<Player> allPlayers = await _databaseController.GetPlayerBase(league.LeagueID);
            foreach (Player player in allPlayers)
            {
                SocketGuildUser user = league.DiscordInformation.DiscordServer.GetUser(player.discordID);
                if (user != null)
                {
                    player.User = league.DiscordInformation.DiscordServer.GetUser(player.discordID);

                    if (!lc.League.RegisteredPlayers.Contains(player))
                    {
                        lc.League.RegisteredPlayers.Add(player);
                    }
                }
                else
                {
                    //_log.Debug($"Could not find SocketGuildUser({player.discordID})");
                }
            }
            
            List<Player> applicants = await _databaseController.GetApplicants(lc.League.LeagueID);
            foreach (Player applicant in applicants)
            {
                SocketGuildUser user = league.DiscordInformation.DiscordServer.GetUser(applicant.discordID);
                if (user != null)
                {
                    applicant.User = user;
                    if (!lc.League.Applicants.Contains(applicant))
                    {
                        lc.League.Applicants.Add(applicant);
                    }
                }
                else
                {
                    //_log.Debug($"Could not find SocketGuildUser({applicant.discordID})");
                }
               
            }
        }

        private async Task LoadMatchHistory(League league)
        {
            _log.Info($"Loading match history of {league.Name}({league.LeagueID})");
            List<Match> matches = await _databaseController.GetMatchHistory(league.LeagueID);
            league.Matches = matches;
            foreach (var match in matches)
            {
                match.League = league;
                LeagueController lc = _leagueCoordinator.GetLeagueController(league.LeagueID);
             
                foreach (var stats in match.PlayerMatchStats)
                {
                    Player player = _leagueCoordinator.GetPlayerBySteamID(stats.SteamID);
                    stats.Match = match;
                    stats.Player = player;

                    Player p = _leagueCoordinator.GetPlayerBySteamID(stats.SteamID);
                    if (p != null)
                        p.Matches.Add(match);
                }
            }
        }

        private async Task LoadLobbies(League league)
        {
            _log.Info($"Loading open lobbies of {league.Name}({league.LeagueID})");
            List<Lobby> lobbies = await _databaseController.GetLobbies(league.LeagueID, league.RegisteredPlayers);
            foreach (var lobby in lobbies)
            {
                lobby.League = league;
                RestTextChannel rtc = await _client.Rest.GetChannelAsync(league.DiscordInformation.Channel.Id) as RestTextChannel;
                if(lobby.StartMessageID != 0)
                    lobby.StartMessage = await rtc.GetMessageAsync(lobby.StartMessageID) as RestUserMessage;
                
                lobby.Host = league.RegisteredPlayers.Find(p => p.SteamID == lobby.HostID);
                if (lobby.HasStarted)
                {
                    lobby.Match = league.Matches.Find(m => m.MatchID == lobby.MatchID);
                    if (lobby.Match == null)
                    {
                        _log.Warn($"Loading Lobby: Couldn't find match #{lobby.MatchID}");
                        continue;
                    }
                       
                    league.LobbyInProgress.Add(lobby);
                }
                else
                {
                    league.Lobby = lobby;
                }

                foreach (var player in lobby.WaitingList)
                {
                    player.CurrentGame = lobby;
                }
            }
        }

        private async Task ServerConnected(SocketGuild server)
        {
            if (!_connectedServers.Contains(server))
            {
                _connectedServers.Add(server);

                if (!IsServerInitialised(server)) {
                    _log.Info("Bot connected to : " + server.Name + "(" + server.Id + ")");
                    await UpdateDiscordInformation(server);
                    foreach(LeagueController lc in _leagueCoordinator.GetLeagueControllersByServer(server))
                    {
                        await LoadPlayerBase(lc.League);
                        await LoadMatchHistory(lc.League);
                        await LoadLobbies(lc.League);
                    }
                }
                else
                {
                    _log.Info("Bot reconnected to : " + server.Name + "(" + server.Id + ")");
                    await ServerValidation(server);
                }
            }
        }

        private async Task UpdateDiscordInformation(SocketGuild server)
        {
            _log.Info("Update discord information of " + server.Name + "(" + server.Id + ")...");
            foreach (var lc in _leagueCoordinator.GetLeagueControllersByServer(server))
            {
                if (lc.League.DiscordInformation != null)
                {
                    if (lc.League.DiscordInformation.DiscordServerId == server.Id)
                    {
                        lc.League.DiscordInformation.DiscordServer = server;
                    }
                }
            }
            _initialisedServers.Add(server);
        }

        //Validates all discord related data if the server disconnected
        //Discord accounts, channel, roles might be deleted while the bot is not connected
        //Working with old discord data might result in a NullPointerException
        public async Task ServerValidation(SocketGuild server)
        {
            foreach (LeagueController lc in _leagueCoordinator.GetLeagueControllersByServer(server))
            {
                _log.Info("Validating server " + server.Name + "(" + server.Id + ")...");

                //TODO: More Validation (Channels, Roles, ...)
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
        }

        private async Task ServerDisconnected(SocketGuild socketGuild)
        {
            _connectedServers.Remove(socketGuild);
            
            //Workaround for https://github.com/RogueException/Discord.Net/issues/960
            Task.Run(async () =>
            {
                Thread.Sleep(MillisecondsTimeout);
                if (!socketGuild.IsConnected && !_connectedServers.Contains(socketGuild)) { 
                    _log.Error($"Could not reconnect to {socketGuild.Name}({socketGuild.Id})");
                    Environment.Exit(1);
                }
            });

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

        }

        private Task Log(LogMessage arg)
        {
            switch (arg.Severity)
            {
                case LogSeverity.Critical:
                    _log.Fatal(arg.Message);
                    if(arg.Exception != null) _log.Error(arg.Exception.ToString());
                    break;
                case LogSeverity.Error:
                    _log.Error(arg.Message);
                    if (arg.Exception != null) _log.Error(arg.Exception.ToString());
                    break;
                case LogSeverity.Warning:
                    _log.Warn(arg.Message);
                    if (arg.Exception != null) _log.Error(arg.Exception.ToString());
                    break;
                case LogSeverity.Debug:
                    _log.Debug(arg.Message);
                    if (arg.Exception != null) _log.Error(arg.Exception.ToString());
                    break;
                case LogSeverity.Info:
                    _log.Info(arg.Message);
                    if (arg.Exception != null) _log.Error(arg.Exception.ToString());
                    break;
            }

            return Task.CompletedTask;
        }

        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = default(Exception);
            ex = (Exception)e.ExceptionObject;
            _log.Error(ex.ToString());
        }
    }
}
