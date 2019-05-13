﻿using System;
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

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace BanjoBotCore
{
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

                            // Restore Lobby Details
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
                }
                else
                {
                    _log.Info("Bot reconnected to : " + server.Name + "(" + server.Id + ")");
                    await UpdateDiscordInformation(server);
                }

            }
        }

        private async Task UpdateDiscordInformation(SocketGuild server)
        {
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

                //TODO: More Validation (Channels, Roles, ...)

                //Validate Players and update discord references
                //Users that left the discord server will be removed from the league
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
            Thread.Sleep(10000);
            if (!_connectedServers.Contains(socketGuild)) { 
                _log.Error($"Could not reconnect to {socketGuild.Name}({socketGuild.Id})");
                Environment.Exit(1);
            }

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
            _log.Error(ex.ToString());
        }
    }
}
