using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BanjoBotCore.Controller;
using Discord;
using Discord.API;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using Microsoft.Extensions.DependencyInjection;
using IChannel = Discord.IChannel;

namespace BanjoBotCore {
    [Name("CommandModule")]
    public class CommandModule : ModuleBase<SocketCommandContext> {

        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(CommandModule));

        private static int saltCounter = 0;
        private String saltIcon = "<:PJSalt:300736349596811265>";
        private Random rnd = new Random();

        private DiscordSocketClient _bot;
        private DatabaseController _database;
        private CommandService _commandService;
        private CommandController _commandController;

        public CommandModule(DatabaseController databaseController, CommandController commandController, DiscordSocketClient bot, CommandService commandService)
        {
            _bot = bot;
            _database = databaseController;
            _commandService = commandService;
            _commandController = commandController;
        }

        [Command("help"), Summary("Shows all Commands"), Alias(new string[] { "h", "?" })]
        public async Task Help()
        {
            String s = "Some commands have options marked with [], e.g. [#league_channel]." +
                " Most of the time the default for an option is the current channel or yourself. \n";
            s += String.Format("{0,-24} {1,-12}\n", "Command", "Description");
            
            foreach (var command in _commandService.Commands.Where(cmd => cmd.Module.Name == "CommandModule"))
            {
               
                String nextMsg = String.Format("{0,-24} {1,-12}\n", String.Join(", ", command.Aliases.ToArray()), command.Summary);
                if (s.Length + nextMsg.Length > 2000)
                {
                    await (await Context.User.GetOrCreateDMChannelAsync()).SendMessageAsync("```" + s + "```");
                    s = "";
                } else
                {
                    s += nextMsg;
                }

            }

            if(s.Length > 0)
                await (await Context.User.GetOrCreateDMChannelAsync()).SendMessageAsync("```" + s + "```");
        }

        [Command("help"), Summary("Shows usage and description of a specific command"), Alias(new string[] { "h", "?" })]
        public async Task Help([Summary("Commandstring")]String commandString)
        {

            foreach (var command in _commandService.Commands)
            {
                if (command.Aliases.Any(commandString.Equals))
                {
                    String s = String.Format("{0,-24} {1,-12}\n", String.Join(", ", command.Aliases.ToArray()), command.Summary);
                    await ReplyAsync("```" + s + "```");
                }               
            }
        }

        [Command("hostgame"), Summary("Creates a new game (if there is no open lobby)"), Alias(new string[] { "host", "hg" }), RequireLeaguePermission]
        public async Task Host() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.HostLobby(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("join"), Summary("Joins the open game"), Alias(new string[] { "j"}), RequireLeaguePermission]
        public async Task Join() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.JoinLobby(Context.Channel, socketGuildChannel, Context.User);  
        }

        [Command("leave"), Summary("Leaves the open game"), Alias(new string[] { "l" }), RequireLeaguePermission]
        public async Task Leave() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.LeaveLobby(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("cancel"), Summary("Cancel the current lobby (only host / moderator)"), Alias(new string[] { "c" }), RequireLeaguePermission]
        public async Task CancelGame() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.CancelLobby(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("votecancel"), Summary("Casts a vote to cancel the open game"), Alias(new string[] { "vc" }), RequireLeaguePermission]
        public async Task VoteCancel() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.VoteCancel(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("startgame"), Summary("Start the game. Host only, requires full game"), Alias(new string[] { "sg" }), RequireLeaguePermission]
        public async Task StartGame() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.StartGame(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("lobby"), Summary("Shows the players that have joined the open game"), Alias(new string[] { "list" }), RequireLeagueChannel]
        public async Task GetPlayers() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.ShowLobby(Context.Channel, socketGuildChannel);
        }

        [Command("showstats"), Summary("!showstats [@Player] | Shows the stats of a player"), Alias(new string[] { "stats", "gs" }), RequireLeaguePermission]
        public async Task ShowStats([Summary("@Player")]IGuildUser guildUser = null, int season = -1) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            IUser user = null;
            if (guildUser == null)
            {
                user = Context.User;
            }
            else
            {
                user = guildUser;
            }
           
            await _commandController.ShowStats(Context.Channel, socketGuildChannel, user, season);
        }

        [Command("showhistory"), Summary("!showhistory [season #] | Shows your match history"), Alias(new string[] { "sh", "history" }), RequireLeaguePermission]
        public async Task ShowHistory([Summary("season #")]int season = -1) {
            SocketGuildChannel socketGuildChannel =  (SocketGuildChannel)Context.Channel;

            await _commandController.GetMatchHistory(Context.Channel, socketGuildChannel, Context.User, season);
        }

        [Command("showhistory"), Summary("!showhistory [season #] | Shows your match history"), Alias(new string[] { "sh", "history" }), RequireLeaguePermission]
        public async Task ShowHistory()
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;

            await _commandController.GetMatchHistory(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("myprofile"), Summary("Shows your stats with more detailed information"), Alias(new string[] { "mp", "profile"}), RequireLeaguePermission]
        public async Task ShowMyProfile() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;

            await _commandController.ShowPlayerProfile(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("getgames"), Summary("Shows the status of all games"), Alias(new string[] { "gg", "games" }), RequireLeagueChannel]
        public async Task ShowGames() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.ListMatches(Context.Channel, socketGuildChannel);
        }


        [Command("won"), Summary("Cast vote for your team as the winner of your current game (post game only)."), Alias(new string[] { "win", "ez" }), RequireLeaguePermission]
        public async Task Win() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.VoteWinner(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("lost"), Summary("Cast vote for your teams as the winner of your current game (post game only)."), Alias(new string[] { "loss" }), RequireLeaguePermission]
        public async Task Lost() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.VoteWinner(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("draw"), Summary("Cast vote for a draw of your current game (post game only)."), RequireLeaguePermission]
        public async Task VoteDraw() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.VoteMatchResult(Context.Channel, socketGuildChannel, Context.User, Teams.Draw);
        }

        [Command("topmmr"), Summary("Shows the top 5 players"), Alias(new string[] { "top", "t" }), RequireLeagueChannel]
        public async Task TopMMR() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.ShowTopMMR(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("rank"), Summary("Shows the top 5 players"), Alias(new string[] { "r" }), RequireLeagueChannel]
        public async Task Rank()
        {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            await _commandController.ShowRank(Context.Channel, socketGuildChannel, Context.User);
        }

        [Command("register"), Summary("!register <SteamID> [#league_channel] | League registration")]
        public async Task Register([Summary("SteamID")]ulong steamid = 0 , [Summary("#Channel")]IChannel channel = null) {
            log.Debug("Registration: " + Context.User.Username + " SteamID(" + steamid + ")");
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }
                       
            await _commandController.RegisterPlayer(Context.Channel, socketGuildChannel, Context.User, steamid);
        }
    
       
        // Fundamental commands by prospekt
        [Command("increasemmr"), Summary("Increases MMR of <user> by the specified <mmr>")]
        public async Task IncreaseMeme([Summary("@Player")]IGuildUser user, [Summary("mmr")]int mmr)
        {
            if (user == null) {
                await ReplyAsync("Please enter the name of the lucky user first!");
            }
            else if (mmr == 0) {
                await ReplyAsync("Please enter the mmr amount to be increased for this lucky user!");
            } else {
				int n = rnd.Next(700, 1700);
                await ReplyAsync("Instructions unclear, setting all players mmr to " + n + " and shutting down bot...");
            }
        }

		[Command("salt"), Summary("How much salt can there be?")]
		public async Task Salt()
		{
            saltCounter = saltCounter + 1;
            switch (saltCounter)
            {
                case 69:
                    await ReplyAsync(String.Format("Well, baby, me so horny. Me so HORNY{1}. Me love you long time. You party{1}?", saltCounter, saltIcon));
                    break;
                case 666:
                    await ReplyAsync(String.Format("6{1} 6{1} 6{1}" +
                    "\nWoe to you, oh earth and sea" +
                    "\nFor the Devil sends the beast with wrath" +
                    "\nBecause he knows the time is short" +
                    "\nLet him who hath understanding" +
                    "\nReckon the number of the beast" +
                    "\nFor it is a human number" +
                    "\nIts number is six hundred and sixty six" +
                    "\n6{1} 6{1} 6{1}", saltCounter, saltIcon));
                    break;
                default:
                    await ReplyAsync(String.Format("There have been {0} {1} occurrences this season", saltCounter, saltIcon));
                    break;
            }
		}

		[Command("penis"), Summary("How big is the schlong of <user>")]
		public async Task Willy([Summary("@Player")]IGuildUser user)
		{
            if (user == null) {
                await ReplyAsync("Missing user's name");
            }
			int n = rnd.Next(1, 20);
			String d = new String('=', n);
			await ReplyAsync("Size: 8" + d + "D");
		}

    

    }

    public class RequireLeaguePermission : PreconditionAttribute {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider map) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)context.Channel;   
            LeagueController lc = LeagueCoordinator.Instance.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await context.Channel.SendMessageAsync("Join a league channel and try again");
                return PreconditionResult.FromError("Join a league channel and try again");
            }

            foreach (var player in lc.League.RegisteredPlayers) {
                if (player.User.Id == context.User.Id) {
                    return PreconditionResult.FromSuccess();
                }
            }

            await context.Channel.SendMessageAsync("You are not registered. Sign up with !register <YourSteamID64> <#league-channel>. For Instructions see #welcome");
            return PreconditionResult.FromError("You are not registered. Sign up with !register <YourSteamID64> <#league-channel>");
        }
    }

    public class RequireLeagueChannel : PreconditionAttribute {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider map) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)context.Channel;
            LeagueController lc = LeagueCoordinator.Instance.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await context.Channel.SendMessageAsync("This is not a league channel");
                return PreconditionResult.FromError("Join a league channel and try again");
            }

            return PreconditionResult.FromSuccess();
        }
    }
}
