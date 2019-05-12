using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using log4net.Core;
using log4net.Repository.Hierarchy;
using log4net;

namespace BanjoBotCore.Controller {
    class CommandHandler
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private const string PREFIX = "!";

        private readonly IServiceProvider _provider;
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _client;
        //private readonly ILogger _logger;

        public CommandHandler(IServiceProvider provider) {
            _provider = provider;
            _client = (DiscordSocketClient)_provider.GetService(typeof(DiscordSocketClient));
            _client.MessageReceived += ProcessCommandAsync;
            _commands = (CommandService)_provider.GetService(typeof(CommandService));
            //var log = _provider.GetService<LogAdaptor>();
            //_commands.Log += log.LogCommand;
            //_logger = _provider.GetService<Logger>().ForContext<CommandService>();
        }

        public async Task ConfigureAsync() {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }

        private async Task ProcessCommandAsync(SocketMessage pMsg) {
            var message = pMsg as SocketUserMessage;
            if (message == null) return;
            if (message.Content.StartsWith("##")) return;

            int argPos = 0;
            if (!ParseTriggers(message, ref argPos)) return;
            var context = new SocketCommandContext(_client, message);
            log.Info(message.Author.Username + ": " + message);
            var result = await _commands.ExecuteAsync(context, argPos, _provider);
            if (!result.IsSuccess)
                log.Info(result.ErrorReason);
            
            //if (result is SearchResult search && !search.IsSuccess)
            //    await message.AddReactionAsync(EmojiExtensions.FromText(":mag_right:"));
            //else if (result is PreconditionResult precondition && !precondition.IsSuccess)
            //    await message.AddReactionAsync(EmojiExtensions.FromText(":no_entry:"));
            //else if (result is ParseResult parse && !parse.IsSuccess)
            //    await message.Channel.SendMessageAsync($"**Parse Error:** {parse.ErrorReason}");
            //else if (result is TypeReaderResult reader && !reader.IsSuccess)
            //    await message.Channel.SendMessageAsync($"**Read Error:** {reader.ErrorReason}");
            //else if (!result.IsSuccess)
            //    await message.AddReactionAsync(EmojiExtensions.FromText(":rage:"));
            //_logger.Debug("Invoked {Command} in {Context} with {Result}", message, context.Channel, result);
        }

        private bool ParseTriggers(SocketUserMessage message, ref int argPos) {
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
    }
}
