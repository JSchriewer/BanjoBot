using Discord;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BanjoBotCore.Controller.Discord
{
    class DiscordController
    {
        private const int MillisecondsTimeout = 30000;
        private static ILog log;

        private DiscordSocketClient _client;
        private List<SocketGuild> _connectedServers;
        private List<SocketGuild> _initialisedServers;
        private LeagueCoordinator _leagueCoordinator;
        private CommandController _commandController;
        private DatabaseController _databaseController;

        private CommandHandler _handler;
        private DiscordMessageDispatcher _messageDispatcher;

        public DiscordController()
        {

        }

        public async Task StartDiscordClient(IConfiguration config)
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug
            });

            _client.Log += Log;
            _client.GuildAvailable += ServerConnected;
            _client.GuildUnavailable += ServerDisconnected;
            _client.UserJoined += UserJoined;
            _client.UserLeft += UserLeft;
            _client.MessageReceived += BotOnMessageReceived;

            log.Info("Initialising...");
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
            _leagueCoordinator = new LeagueCoordinator();
            _messageDispatcher = new DiscordMessageDispatcher();
            _commandController = new CommandController(_messageDispatcher, _leagueCoordinator);
            _databaseController = new DatabaseController();

            Thread t = new Thread(new ThreadStart(_messageDispatcher.Run));
            t.Start();

            var serviceProvider = ConfigureServices();
            _handler = new CommandHandler(serviceProvider);

            await _handler.ConfigureAsync();
        }

        private IServiceProvider ConfigureServices()
        {
            CommandServiceConfig commandConfig = new CommandServiceConfig { CaseSensitiveCommands = false, ThrowOnError = false };
            //commandConfig.DefaultRunMode = RunMode.Async;
            CommandService commandService = new CommandService(commandConfig);
            var services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(commandService)
                .AddSingleton(_databaseController)
                .AddSingleton(_commandController) //Should not be a singleton service, multiple instance should be fine
                .AddSingleton<IConfiguration>(_config);
            var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
            return provider;
        }

    }
}
