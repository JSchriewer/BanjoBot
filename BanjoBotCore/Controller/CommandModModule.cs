using BanjoBotCore;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBotCore.Controller
{
    [RequireModPermission]
    public class CommandModModule : ModuleBase<SocketCommandContext>
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(CommandModule));

        private DiscordSocketClient _bot;
        private DatabaseController _database;
        private CommandService _commandService;
        private CommandController _commandController;

        public CommandModModule(DatabaseController databaseController, CommandController commandController, DiscordSocketClient bot, CommandService commandService)
        {
            _bot = bot;
            _database = databaseController;
            _commandService = commandService;
            _commandController = commandController;
        }

        // Moderator commands
        [Command("end"), Summary("(Moderator) !end <match-nr #> <Red | Blue | Draw> | Ends a game. The Match_ID can be found via !gg command, it is the number in the brackets")]
        public async Task EndGame([Summary("matchID")]int match, [Summary("team")]Teams team)
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.CloseLobbyByModerator(Context.Channel, socketGuildChannel, Context.User, match, team);
        }

        [Command("recreatelobby"), Summary("(Moderator) !rcl <Match_ID> @Player_to_remove | Kicks a Player from a started Match and reopens the Lobby"), Alias(new string[] { "rcl" })]
        public async Task ReCreateLobby([Summary("matchID")]int match, [Summary("Player to remove")]IGuildUser player)
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.ReCreateLobby(Context.Channel, socketGuildChannel, match, player);
        }

        [Command("kick"), Summary("(Moderator) !kick @Player | Kicks the player from the current lobby")]
        public async Task Kick([Summary("@Player")]IGuildUser guildUser)
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.KickPlayer(Context.Channel, socketGuildChannel, guildUser);
        }

        //TODO: Cascade 
        //TODO: CHange Discord
        //[Command("changeSteam"), Summary("Changes the steamID of a user. !cs @player (moderator)"), Alias(new string[] { "cs" })]
        //public async Task ChangeSteam([Summary("@Player")]IGuildUser guildUser, ulong SteamID)
        //{
        //    bool hasPermission = false;
        //    foreach (var lc in _leagueCoordinator.GetLeagueControllersByServer((SocketGuild)Context.Guild)) {
        //        if (CheckModeratorPermission((SocketGuildUser)Context.User, lc)) {
        //            hasPermission = true;
        //        }
        //    }
        //    if (!hasPermission) {
        //        return;
        //    }

        //    Player player = _leagueCoordinator.GetPlayerByDiscordID(guildUser.Id);
        //    if (player == null) {
        //        await ReplyAsync("Player not found");
        //        return;
        //    }

        //    player.SteamID = SteamID;
        //    await _database.UpdatePlayer(player);
        //    await ReplyAsync("SteamID changed");
        //}

        [Command("accept"), Summary("(Moderator) !accept @Player [#league_channel] | Accepts a applicant")]
        public async Task Accept([Summary("@Player")]IGuildUser guildUser, [Summary("#Channel")]IChannel channel = null)
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

            await _commandController.AcceptApplicant(Context.Channel, socketGuildChannel, guildUser);

        }

        [Command("decline"), Summary("(Moderator) !decline @Player [reasoning] [#league_channel] | Declines a applicant")]
        public async Task Accept([Summary("@Player")]IGuildUser guildUser, string reasoning = "", [Summary("#Channel")]IChannel channel = null)
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

            await _commandController.DeclineApplicant(Context.Channel, socketGuildChannel, guildUser, reasoning);
        }

        [Command("listleagues"), Summary("(Moderator) !listleagues | Lists all leagues and details")]
        public async Task ListLeagues()
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.ListLeagues(Context.Channel, socketGuildChannel);
        }

        [Command("listapplicants"), Summary("(Moderator) !listapplicants | Lists all applicants")]
        public async Task ListApplicants()
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.ListApplicants(Context.Channel, socketGuildChannel);
        }

        [Command("listplayers"), Summary("(Moderator) !listplayers #league_channel | Lists all registered players")]
        public async Task ListPlayer([Summary("#Channel")]IChannel channel)
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)channel;
            await _commandController.ListPlayers(Context.Channel, socketGuildChannel);

        }
        [Command("whois"), Summary("!whois <discord_id> | Finds username by discord-id")]
        public async Task WhoIs(ulong id)
        {
            var user = _bot.GetUser(id);
            if (user == null)
            {
                await Context.Channel.SendMessageAsync("User not found");
            }
            else
            {
                await Context.Channel.SendMessageAsync(user.Username);
            }
        }

    

    }

    public class RequireModPermission : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider map)
        {
            if (!(context.User is SocketGuildUser user))
            {
                return PreconditionResult.FromError("You are not a guild member");
            }

           
            bool hasPermission = false;
            foreach (var leaguecontroller in LeagueCoordinator.Instance.GetLeagueControllersByServer((SocketGuild)context.Guild))
            {
                if (CheckModeratorPermission(user, leaguecontroller))
                {
                    hasPermission = true;
                }
            }
            if (!hasPermission)
            {
                return PreconditionResult.FromError("No permission");
            }

            return PreconditionResult.FromSuccess();
        }

        public bool CheckModeratorPermission(SocketGuildUser user, LeagueController lc)
        {
            if (lc.League.DiscordInformation.ModeratorRole != null && (user.Roles.Contains(lc.League.DiscordInformation.ModeratorRole) || user.GuildPermissions.Administrator))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
