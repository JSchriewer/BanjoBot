﻿using System;
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
using Discord;

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
            _commands = (CommandService)_provider.GetService(typeof(CommandService));

            //var log = _provider.GetService<LogAdaptor>();
            //_commands.Log += log.LogCommand;
            //_logger = _provider.GetService<Logger>().ForContext<CommandService>();
        }

        public async Task ConfigureAsync() {
            _client.MessageReceived += ProcessCommandAsync;
            _commands.CommandExecuted += OnCommandExecutedAsync;
            _commands.Log += LogAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }

        private async Task ProcessCommandAsync(SocketMessage pMsg) {
            var message = pMsg as SocketUserMessage;
            if (message == null) return;
            if (pMsg.Channel is IDMChannel) return;
            if (message.Content.StartsWith("##")) return;

            int argPos = 0;
            if (!ParseTriggers(message, ref argPos)) return;     
            var context = new SocketCommandContext(_client, message);
            log.Info($"{context.User.Username} tries to execute '{context.Message}' in {context.Channel}.");
            var result = await _commands.ExecuteAsync(context, argPos, _provider);
            
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

        public async Task LogAsync(LogMessage logMessage)
        {
            // This casting type requries C#7
            if (logMessage.Exception is CommandException cmdException)
            {
                // We can tell the user that something unexpected has happened
                await cmdException.Context.Channel.SendMessageAsync("Something went catastrophically wrong!");

                // We can also log this incident
                log.Error($"{cmdException.Context.User} failed to execute '{cmdException.Command.Name}' in {cmdException.Context.Channel}.");
                log.Error(cmdException.ToString());
            }
        }

        public async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            // We have access to the information of the command executed,
            // the context of the command, and the result returned from the
            // execution in this event.

            // We can tell the user what went wrong
            if (!string.IsNullOrEmpty(result?.ErrorReason))
            {
                await context.Channel.SendMessageAsync(result.ErrorReason);
            }

            // ...or even log the result (the method used should fit into
            // your existing log handler)
            var commandName = command.IsSpecified ? command.Value.Name : "A command";
            log.Info("CommandExecution by " + context.User.Username + " : !" + commandName);
        }
    }
}