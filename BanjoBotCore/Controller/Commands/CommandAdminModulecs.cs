using Discord;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BanjoBotCore.Controller
{
    [Name("AdminModule")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class CommandAdminModulecs : ModuleBase<SocketCommandContext>
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(CommandModule));

        private DiscordSocketClient _bot;
        private DatabaseController _database;
        private CommandService _commandService;
        private CommandController _commandController;

        public CommandAdminModulecs(DatabaseController databaseController, CommandController commandController, DiscordSocketClient bot, CommandService commandService)
        {
            _bot = bot;
            _database = databaseController;
            _commandService = commandService;
            _commandController = commandController;
        }

        [Command("adminhelp"), Summary("Shows all Commands"), Alias(new string[] { "ah", "a?" })]
        public async Task Help()
        {
            String s = "Some commands have options marked with [], e.g. [#league_channel]." +
                " Most of the time the default for an option is the current channel or yourself. \n";
            s += String.Format("{0,-24} {1,-12}\n", "Command", "Description");

            foreach (var command in _commandService.Commands.Where(cmd => cmd.Module.Name == "AdminModule"))
            {
                String nextMsg = String.Format("{0,-24} {1,-12}\n", String.Join(", ", command.Aliases.ToArray()), command.Summary);
                if (s.Length + nextMsg.Length > 2000)
                {
                    await (await Context.User.GetOrCreateDMChannelAsync()).SendMessageAsync("```" + s + "```");
                    s = "";
                }
                else
                {
                    s += nextMsg;
                }
            }

            if (s.Length > 0)
                await (await Context.User.GetOrCreateDMChannelAsync()).SendMessageAsync("```" + s + "```");
        }

        [Command("setmodchannel"), Summary("(Admin) !setmodchannel #ModChannel [#league_channel] | Sets moderator channel"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetModChannel([Summary("#ModChannel")]IChannel modChannel, [Summary("#LeagueChannel")]IChannel leagueChannel = null)
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            if (leagueChannel != null)
            {
                socketGuildChannel = (SocketGuildChannel)leagueChannel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.SetModChannel(Context.Channel, socketGuildChannel, modChannel);
        }

        [Command("autoaccept"), Summary("(Admin) !autoaccept <true/false> [#league_channel] | Registrations do not have to be manually accepted by a moderator if activated"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAutoAccept([Summary("True/False")]bool autoAccept, [Summary("#Channel")]IChannel channel = null)
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.SetAutoAccept(Context.Channel, socketGuildChannel, autoAccept);
        }

        [Command("steamregister"), Summary("(Admin) !steamregister <true/false> [#league_channel] | Enables steam requirement"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSteamRegister([Summary("True/False")]bool steamregister, [Summary("#Channel")]IChannel channel = null)
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.SetSteamRegister(Context.Channel, socketGuildChannel, steamregister);
        }

        [Command("createleague"), Summary("(Admin) !createLeague <Name> [#league_channel] | Creates a league"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task CreateLeague([Summary("LeagueName")]string name, [Summary("#Channel")]IChannel channel = null)
        {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.CreateLeague(Context.Channel, socketGuildChannel, (SocketGuild)Context.Guild, name);
        }

        [Command("deleteleague"), Summary("(Admin) !deleteleague [#league_channel] | Deletes a league"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeleteLeague([Summary("#Channel")]IChannel channel = null)
        {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.DeleteLeague(Context.Channel, socketGuildChannel);
        }

        [Command("setchannel"), Summary("(Admin) !setchannel #new_league_channel [#old_league_channel] | Sets the league channel"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetChannel([Summary("#Channel")]IChannel newChannel, [Summary("#Channel")]IChannel oldChannel = null)
        {
            SocketGuildChannel socketGuildChannel = null;
            if (oldChannel != null)
            {
                socketGuildChannel = (SocketGuildChannel)oldChannel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.SetChannel(Context.Channel, (SocketGuildChannel)newChannel, socketGuildChannel);
        }

        [Command("setrole"), Summary("(Admin) !setrole [@Role] [#league_channel] | Sets or delete the league role"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetRole([Summary("@Role")]IRole role = null, [Summary("#Channel")]IChannel channel = null)
        {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.SetLeagueRole(Context.Channel, socketGuildChannel, role);
        }

        [Command("setmodrole"), Summary("(Admin) !setmodrole [@Role] [#league_channel] | Sets or delete the mod role"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetModRole([Summary("@Role")]IRole role = null, [Summary("#Channel")]IChannel channel = null)
        {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.SetModRole(Context.Channel, socketGuildChannel, role);
        }

        [Command("startseason"), Summary("(Admin) !startseason [#league_channel] | Ends the current season and starts a new season"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task StartSeason([Summary("#Channel")]IChannel channel = null)
        {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            await _commandController.StartNewSeason(Context.Channel, socketGuildChannel);
        }
    }
}