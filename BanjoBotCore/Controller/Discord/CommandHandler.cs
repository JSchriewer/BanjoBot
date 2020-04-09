using Discord;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace BanjoBotCore.Controller
{
    internal class CommandHandler
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private const string PREFIX = "!";

        private readonly IServiceProvider _provider;
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _client;

        public CommandHandler(IServiceProvider provider)
        {
            _provider = provider;
            _client = (DiscordSocketClient)_provider.GetService(typeof(DiscordSocketClient));
            _commands = (CommandService)_provider.GetService(typeof(CommandService));

            //var log = _provider.GetService<LogAdaptor>();
            //_commands.Log += log.LogCommand;
            //_logger = _provider.GetService<Logger>().ForContext<CommandService>();
        }

        public async Task ConfigureAsync()
        {
            _client.MessageReceived += ProcessCommandAsync;
            _commands.CommandExecuted += OnCommandExecutedAsync;
            _commands.Log += LogAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }

        private async Task ProcessCommandAsync(SocketMessage pMsg)
        {
            var message = pMsg as SocketUserMessage;
            if (message == null) return;
            if (pMsg.Channel is IDMChannel) return;
            if (message.Content.StartsWith("##")) return;

            int argPos = 0;
            if (!ParseTriggers(message, ref argPos)) return;
            var context = new SocketCommandContext(_client, message);
            log.Info($"{context.User.Username} tries to execute '{context.Message}' in {context.Channel}.");
            var result = await _commands.ExecuteAsync(context, argPos, _provider);
        }

        private bool ParseTriggers(SocketUserMessage message, ref int argPos)
        {
            bool flag = false;
            if (message.HasMentionPrefix(_client.CurrentUser, ref argPos))
            {
                flag = true;
            }
            else if (message.HasStringPrefix(PREFIX, ref argPos))
            {
                flag = true;
            }

            return flag;
        }

        public async Task LogAsync(LogMessage logMessage)
        {
            if (logMessage.Exception is CommandException cmdException)
            {
                log.Error($"{cmdException.Context.User} failed to execute '{cmdException.Command.Name}' in {cmdException.Context.Channel}.");
                log.Error(cmdException.ToString());
            }
        }

        public async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!string.IsNullOrEmpty(result?.ErrorReason))
            {
                await context.Channel.SendMessageAsync(result.ErrorReason);
            }

            var commandName = command.IsSpecified ? command.Value.Name : "A command";
            log.Info("CommandExecution by " + context.User.Username + " : !" + commandName);
        }
    }
}