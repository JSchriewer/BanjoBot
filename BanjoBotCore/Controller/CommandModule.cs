using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.API;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using Microsoft.Extensions.DependencyInjection;
using IChannel = Discord.IChannel;

namespace BanjoBot {
    public class CommandModule : ModuleBase<SocketCommandContext> {

        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(CommandModule));
        private const string RULE_URL = "https://docs.google.com/document/d/1ibvVJ1o7CSuPl8AfdEJN4j--2ivC93XOKulVq28M_BE";
        private const string STEAM_PROFILE_URL = "https://steamcommunity.com/profiles/";

        private DiscordSocketClient _bot;
        private LeagueCoordinator _leagueCoordinator;
        private DatabaseController _database;
        private CommandService _commandService;
		private Random rnd = new Random();
        //TODO: not working, unpin message on accept
        private Dictionary<ulong, IUserMessage> _signups = new Dictionary<ulong, IUserMessage>(); 

        public CommandModule(DatabaseController databaseController, DiscordSocketClient bot, CommandService commandService)
        {
            _bot = bot;
            _leagueCoordinator = LeagueCoordinator.Instance;
            _database = databaseController;
            _commandService = commandService;
        }

        [Command("help"), Summary("Shows all Commands"), Alias(new string[] { "h", "?" })]
        public async Task Help()
        {
            String s = "Some commands have options marked with [], e.g. [#league_channel]." +
                " Most of the time the default for an option is the current channel or yourself. \n";
            s += String.Format("{0,-24} {1,-12}\n", "Command", "Description");
            int count = 0;
            foreach (var command in _commandService.Commands)
            {
                count++;
                s += String.Format("{0,-24} {1,-12}\n", String.Join(", ", command.Aliases.ToArray()), command.Summary);
                if (count % 15 == 0)
                {
                    await (await Context.User.GetOrCreateDMChannelAsync()).SendMessageAsync("```" + s + "```");
                    count = 0;
                    s = "";
                }

            }
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
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.CreateLobby(Context.Channel, player);
        }

        [Command("join"), Summary("Joins the open game"), Alias(new string[] { "j"}), RequireLeaguePermission]
        public async Task Join() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.JoinLobby(Context.Channel, player);  
        }

        [Command("leave"), Summary("Leaves the open game"), Alias(new string[] { "l" }), RequireLeaguePermission]
        public async Task Leave() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.LeaveLobby(Context.Channel, player);
        }

        [Command("cancel"), Summary("Cancel the current lobby (only host / moderator)"), Alias(new string[] { "c" }), RequireLeaguePermission]
        public async Task CancelGame() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.CancelLobby(Context.Channel,player);
        }

        [Command("votecancel"), Summary("Casts a vote to cancel the open game"), Alias(new string[] { "vc" }), RequireLeaguePermission]
        public async Task VoteCancel() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.VoteCancel(Context.Channel, player);
        }

        [Command("startgame"), Summary("Start the game. Host only, requires full game"), Alias(new string[] { "sg" }), RequireLeaguePermission]
        public async Task StartGame() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.StartGame(Context.Channel, player);
        }

        [Command("lobby"), Summary("Shows the players that have joined the open game"), Alias(new string[] { "list" }), RequireLeagueChannel]
        public async Task GetPlayers() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            await lc.ShowLobby(Context.Channel);
        }

        [Command("showstats"), Summary("!showstats [@Player] | Shows the stats of a player"), Alias(new string[] { "stats", "gs" }), RequireLeaguePermission]
        public async Task ShowStats([Summary("@Player")]IGuildUser guildUser = null, int season = -1) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = null;
            if (guildUser == null)
            {
                player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            }
            else
            {
                player = lc.League.GetPlayerByDiscordID(guildUser.Id);
            }
            
            await lc.ShowStats(Context.Channel, player, season);
        }

        [Command("showhistory"), Summary("!showhistory [season #] | Shows your match history"), Alias(new string[] { "sh", "history" }), RequireLeaguePermission]
        public async Task ShowStats([Summary("season #")]int season = -1) {
            SocketGuildChannel socketGuildChannel =  (SocketGuildChannel)Context.Channel;
            
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);

            if (season != -1) {
                await lc.GetMatchHistory(player,season);
            }
            else {
                await lc.GetMatchHistory(player);
            }
        }

        [Command("myprofile"), Summary("Shows your stats with more detailed information"), Alias(new string[] { "mp", "profile"}), RequireLeaguePermission]
        public async Task ShowMyStats() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);

            await lc.ShowPlayerProfile(player);
        }

        [Command("getgames"), Summary("Shows the status of all games"), Alias(new string[] { "gg", "games" }), RequireLeagueChannel]
        public async Task ShowGames() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            await lc.ShowGames(Context.Channel);
        }


        [Command("won"), Summary("Cast vote for your team as the winner of your current game (post game only)."), Alias(new string[] { "win", "ez" }), RequireLeaguePermission]
        public async Task Win() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            if (!player.IsIngame())
            {
                await ReplyAsync("You are not ingame");
                return;
            }

            Teams team = player.CurrentGame.BlueList.Contains(player) ? Teams.Blue : Teams.Red;
            await lc.VoteWinner(Context.Channel, player, team);
        }

        [Command("lost"), Summary("Cast vote for your teams as the winner of your current game (post game only)."), Alias(new string[] { "loss" }), RequireLeaguePermission]
        public async Task Lost() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            if (!player.IsIngame())
            {
                await ReplyAsync("You are not ingame");
                return;
            }

            Teams team = player.CurrentGame.BlueList.Contains(player) ? Teams.Red : Teams.Blue;
            await lc.VoteWinner(Context.Channel, player, team);
        }

        [Command("draw"), Summary("Cast vote for a draw of your current game (post game only)."), RequireLeaguePermission]
        public async Task VoteDraw() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.VoteWinner(Context.Channel, player, Teams.Draw);
        }

        [Command("topmmr"), Summary("Shows the top 5 players"), Alias(new string[] { "top", "t" }), RequireLeagueChannel]
        public async Task TopMMR() {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            await lc.ShowTopMMR(Context.Channel);
        }

        [Command("register"), Summary("!register <SteamID> [#league_channel] | League registration")]
        public async Task Register([Summary("SteamID")]ulong steamid = 0 , [Summary("#Channel")]IChannel channel = null) {
            log.Debug("Try Register: " + Context.User.Username + " SteamID(" + steamid + ")");
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            log.Debug("Try Register: GetLeagueController");
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            log.Debug("Try Register: GetPlayerByDiscordID ID="+ Context.User.Id);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            if (player != null) {
                await ReplyAsync("You are already registered");
                return;
            }

            log.Debug("Try Register: GetApplicantByDiscordID ID=" + Context.User.Id);
            player = lc.League.GetApplicantByDiscordID(Context.User.Id);
            if (player != null)
            {
                await ReplyAsync("You are already signed up, wait for the approval by a moderator");
                return;
            }

            log.Debug("Try Register: SteamCheck");
            if (lc.League.DiscordInformation.NeedSteamToRegister)
            {
                if (steamid == 0) {
                    await ReplyAsync("Missing steamID. Please use !register <YourSteamID64>");
                    return;
                }

                if (!steamid.ToString().StartsWith("7656")) {
                    await ReplyAsync("Thats not a valid steamid64, please follow the instructions in #welcome");
                    return;
                }

                log.Debug("Try Register: Search for duplicate steam id");
                foreach (var league in _leagueCoordinator.LeagueControllers)
                {
                    foreach (var regplayer in league.League.RegisteredPlayers)
                    {
                        if (regplayer.SteamID == steamid && regplayer.discordID != Context.User.Id)
                        {
                            await ReplyAsync("The SteamID is already in use, please contact a moderator");
                            return;
                        }
                    }
                }
            }

            log.Debug("Registrationdata of " + Context.User.Username + " is valid");
            player = _leagueCoordinator.GetPlayerByDiscordID(Context.User.Id);
            if (player == null)
            {
                log.Debug("Creating new player");
                player = new Player((SocketGuildUser) Context.User, steamid);
                await _database.InsertNewPlayer(player);

            }

            if (lc.League.DiscordInformation.AutoAccept)
            {
                await lc.RegisterPlayer(player);
                await ReplyAsync( player.User.Mention + "You are registered now. Use !help to see the command list");
            }
            else
            {
                log.Debug("Add applicant" + Context.User.Username + " to " + lc.League.Name);
                lc.League.Applicants.Add(player);
                await _database.InsertSignupToLeague(player.SteamID, lc.League);
                await ReplyAsync("You are signed up now. Wait for the approval by a moderator");
                if (lc.League.DiscordInformation.ModeratorChannel != null)
                {
                    //TODO: _signups is not working correctly
                    IUserMessage message = await((ITextChannel) lc.League.DiscordInformation.ModeratorChannel).SendMessageAsync("New applicant: " + player.User.Mention + "\t" + STEAM_PROFILE_URL + player.SteamID +"\tLeague: " + lc.League.Name);
                    _signups.Add(player.User.Id, message);
                    await message.PinAsync();
                }
            }
        }
    
        // Moderator commands
        [Command("end"), Summary("(Moderator) !end <match-nr #> <Red | Blue | Draw> | Ends a game. The Match_ID can be found via !gg command, it is the number in the brackets"), RequireLeaguePermission]
        public async Task EndGame([Summary("matchID")]int match, [Summary("team")]Teams team) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!CheckModeratorPermission((SocketGuildUser)Context.User,lc)) {
                return;
            }

            await lc.EndGameByModerator(Context.Channel, match,team);
        }

        [Command("recreatelobby"), Summary("(Moderator) !rcl <Match_ID> @Player_to_remove | Kicks a Player from a started Match and reopens the Lobby"), Alias(new string[] { "rcl"}), RequireLeaguePermission]
        public async Task ReCreateLobby([Summary("matchID")]int match, [Summary("Player to remove")]IGuildUser player) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!CheckModeratorPermission((SocketGuildUser)Context.User, lc)) {
                return;
            }

            await lc.ReCreateLobby(Context.Channel,match, player);
        }

        [Command("kick"), Summary("(Moderator) !kick @Player | Kicks the player from the current lobby")]
        public async Task Kick([Summary("@Player")]IGuildUser guildUser) {
            LeagueController lc = _leagueCoordinator.GetLeagueController((SocketGuildChannel)Context.Channel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!CheckModeratorPermission((SocketGuildUser)Context.User, lc)) {
                return;
            }

            Player player = null;
            player = lc.League.GetPlayerByDiscordID(guildUser.Id);
            if (player == null) {
                await ReplyAsync("player not found");
                return;
            }

            await lc.KickPlayer(Context.Channel, player);
        }

        //TODO: Cascade 

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
        public async Task Accept([Summary("@Player")]IGuildUser guildUser, [Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }
            
            if (!CheckModeratorPermission((SocketGuildUser)Context.User, lc)) {
                return;
            }

            Player player = null;
            player = lc.League.GetApplicantByDiscordID(guildUser.Id);
            if (player == null) {
                await ReplyAsync("applicant not found");
                return;
            }

            await lc.RegisterPlayer(player);
            await ReplyAsync(player.User.Mention + "You got a private message!");
            await (await (player.User as IGuildUser).GetOrCreateDMChannelAsync()).SendMessageAsync("Your registration for " + lc.League.Name + " got approved.\nYou can now start playing!\n\n If you need help, ask a moderator or use !help \n\n Note: Please make sure you read the rules, you can find them in the channel #rules\n");
            await _signups[player.User.Id].UnpinAsync();
            _signups.Remove(player.User.Id);
        }

        [Command("decline"), Summary("(Moderator) !decline @Player [reasoning] [#league_channel] | Declines a applicant")]
        public async Task Accept([Summary("@Player")]IGuildUser guildUser, string reasoning = "", [Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!CheckModeratorPermission((SocketGuildUser)Context.User, lc)) {
                return;
            }

            if (guildUser == null) {
                await ReplyAsync("usage: !decline @player [reason] [#channel] ");
                return;
            }

            Player player = null;
            player = lc.League.GetApplicantByDiscordID(guildUser.Id);
            if (player == null) {
                await ReplyAsync("applicant not found");
                return;
            }

            lc.League.Applicants.Remove(player);
            await _database.DeleteRegistration(player.SteamID, lc.League);

            await ReplyAsync(player.User.Mention + "You got a private message!");
            if(!reasoning.Equals(""))
                await (await (player.User as IGuildUser).GetOrCreateDMChannelAsync()).SendMessageAsync("Your registration for " + lc.League.Name + " got declined.\n Reason: " +  reasoning +"\nTry again or contact a moderator");
            else
                await (await (player.User as IGuildUser).GetOrCreateDMChannelAsync()).SendMessageAsync("Your registration for " + lc.League.Name + " got declined.\nTry again or contact a moderator");
        }

        [Command("listleagues"), Summary("(Moderator) !listleagues | Lists all leagues and details")]
        public async Task ListLeagues() {

            bool hasPermission = false;
            foreach (var lc in _leagueCoordinator.GetLeagueControllersByServer((SocketGuild) Context.Guild)) {
                if (CheckModeratorPermission((SocketGuildUser) Context.User,lc))
                {
                    hasPermission = true;
                }
            }
            if (!hasPermission) {
                return;
            }

            object[] args = new object[] {"ID", "Name",
                    "Channel", "Role", "AutoAccept", "Steam",
                    "Season", "Matches", "Players",
                    "Applicants","ModRole"};
            String s = String.Format(
                        "{0,-4} {1,-10} {2,-10} {3,-10} {4,-12} {5,-8} {6,-8} {7,-8} {8,-8} {9,-8} {10,-10}\n", args);
            foreach (var lc in _leagueCoordinator.GetLeagueControllersByServer((SocketGuild)Context.Guild)) {
                string channel = lc.League.DiscordInformation.Channel != null ? lc.League.DiscordInformation.Channel.Name : "none";
                string role = lc.League.DiscordInformation.LeagueRole != null ? lc.League.DiscordInformation.LeagueRole.Name : "none";
                string modrole = lc.League.DiscordInformation.ModeratorRole != null ? lc.League.DiscordInformation.ModeratorRole.Name : "none";
                args = new object[] {lc.League.LeagueID, lc.League.Name,
                    channel, role, lc.League.DiscordInformation.AutoAccept, lc.League.DiscordInformation.NeedSteamToRegister,
                    lc.League.Season, lc.League.GameCounter, lc.League.RegisteredPlayers.Count,
                    lc.League.Applicants.Count, modrole};
                s += String.Format("{0,-4} {1,-10} {2,-10} {3,-12} {4,-10} {5,-8} {6,-8} {7,-8} {8,-8} {9,-8} {10,-10}\n", args);
            }
            await ReplyAsync("```" + s + "```");
        }

        [Command("listapplicants"), Summary("(Moderator) !listapplicants | Lists all applicants")]
        public async Task ListApplicants() {
            bool hasPermission = false;
            foreach (var lc in _leagueCoordinator.GetLeagueControllersByServer((SocketGuild)Context.Guild)) {
                if (CheckModeratorPermission((SocketGuildUser)Context.User, lc)) {
                    hasPermission = true;
                }
            }
            if (!hasPermission) {
                return;
            }

            object[] args = new object[] { "DiscordID", "Name", "SteamID","Steam Profile", "League" };
            String s = String.Format(
                        "{0,-24} {1,-12} {2,-24} {3,-64} {4,-24}\n", args);
            foreach (var lc in _leagueCoordinator.GetLeagueControllersByServer((SocketGuild) Context.Guild)) {
                foreach (var leagueApplicant in lc.League.Applicants) {
                    if (s.Length > 1800) {
                        await ReplyAsync("```" + s + "```");
                        s = "";
                    }
                    string name = leagueApplicant.User.Username.Length >= 13 ? leagueApplicant.User.Username.Substring(0, 12) : leagueApplicant.User.Username;
                    args = new object[] { leagueApplicant.User.Id,name, leagueApplicant.SteamID, STEAM_PROFILE_URL + leagueApplicant.SteamID, lc.League.Name };
                    s += String.Format("{0,-24} {1,-12} {2,-24} {3,-64} {4,-24}\n", args);
                }
            }
            await ReplyAsync("```" + s + "```");
        }

        [Command("whois"), Summary("!whois <discord_id> | Finds username by discord-id")]
        public async Task WhoIs(ulong id)
        {
            var user = _bot.GetUser(id);
            if(user == null)
            {
                await Context.Channel.SendMessageAsync("User not found");
            } else
            {
                await Context.Channel.SendMessageAsync(user.Username);
            }
        }

        [Command("listplayers"), Summary("(Moderator) !listplayers #league_channel | Lists all registered players")]
        public async Task ListPlayer([Summary("#Channel")]IChannel channel) {
            bool hasPermission = false;
            foreach (var leaguecontroller in _leagueCoordinator.GetLeagueControllersByServer((SocketGuild)Context.Guild)) {
                if (CheckModeratorPermission((SocketGuildUser)Context.User, leaguecontroller)) {
                    hasPermission = true;
                }
            }
            if (!hasPermission) {
                return;
            }

            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)channel;
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("League not found");
                return;
            }

            Console.WriteLine("listplayers");
            object[] args = new object[] {"DiscordID", "Name", "SteamID", "Matches", "M+D", "Wins","Losses","Mmr"};
            String s = String.Format(
                "{0,-24} {1,-14} {2,-24} {3,-8} {4,-8} {5,-10} {6,-8} {7,-8}\n", args);


            foreach (var player in lc.League.RegisteredPlayers)
            {
                Console.Write("player: " +player.discordID + " steam: " +player.SteamID);
                if (s.Length > 1800)
                {          
                    await ReplyAsync("```" + s + "```");
                    s = "";
                }
                PlayerStats Stats = player.GetLeagueStat(lc.League.LeagueID, lc.League.Season);
                string name = player.User.Username.Length >= 13 ? player.User.Username.Substring(0, 12) : player.User.Username;
                args = new object[]
                {
                    player.User.Id, name, player.SteamID,Stats.MatchCount,
                    player.Matches.Count,Stats.Wins, Stats.Losses, Stats.MMR
                };

                s += String.Format("{0,-24} {1,-14} {2,-24} {3,-8} {4,-8} {5,-10} {6,-8}{7,-8}\n", args);


            }
                
            await ReplyAsync("```" + s + "```");
       
        }


        // Admin commands
        [Command("setmodchannel"), Summary("(Admin) !setmodchannel #ModChannel [#league_channel] | Sets moderator channel"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAutoAccept([Summary("#ModChannel")]IChannel modChannel, [Summary("#LeagueChannel")]IChannel leagueChannel) {
            LeagueController lc =_leagueCoordinator.GetLeagueController((SocketGuildChannel) leagueChannel);
            if (lc == null)
            {
                await ReplyAsync("League not found");
                return;
            }

            lc.League.DiscordInformation.ModeratorChannel = (SocketGuildChannel) modChannel;
            await _database.UpdateLeague(lc.League);
            await ReplyAsync("Moderator channel set to: " + modChannel.Name);
        }

        [Command("autoaccept"), Summary("(Admin) !autoaccept <true/false> [#league_channel] | Registrations do not have to be manually accepted by a moderator if activated"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAutoAccept([Summary("True/False")]bool  autoAccept, [Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await ReplyAsync("Join a league channel first or pass a channel as a parameter #channel.");
                return;
            }

            lc.League.DiscordInformation.AutoAccept = autoAccept;
            await _database.UpdateLeague(lc.League);
            await ReplyAsync("Autoaccept set to " + autoAccept);
        }

        [Command("steamregister"), Summary("(Admin) !steamregister <true/false> [#league_channel] | Enables steam requirement"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSteamRegister([Summary("True/False")]bool steamregister, [Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("Join a league channel first or pass a channel as a parameter #channel.");
                return;
            }

            lc.League.DiscordInformation.NeedSteamToRegister = steamregister;
            await _database.UpdateLeague(lc.League);
        }

        [Command("createleague"), Summary("(Admin) !createLeague <Name> [#league_channel] | Creates a league"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task CreateLeague([Summary("LeagueName")]string name, [Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                 socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc != null) {
                await ReplyAsync("This channel is already assigned to another league.");
                return;
            }

            int leagueID = await _database.InsertNewLeague();
            Console.WriteLine(leagueID);
            Console.WriteLine(name);
            League league = new League(leagueID,name ,1);
            league.DiscordInformation = new DiscordInformation(Context.Guild.Id, (SocketGuild)Context.Guild, socketGuildChannel.Id);
            _leagueCoordinator.AddLeague(league);
            await _database.UpdateLeague(league);
            await ReplyAsync("League created.");
            
            
        }

        [Command("deleteleague"), Summary("(Admin) !deleteleague [#league_channel] | Deletes a league"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeleteLeague([Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc != null)
            {
                await _database.DeleteLeague(lc.League);
                _leagueCoordinator.DeleteLeague(lc);
                await ReplyAsync("League deleted.");
            }
            else {
                await ReplyAsync("This channel is not assigned to league.");
            }
            
        }

        [Command("setchannel"), Summary("(Admin) !setchannel #new_league_channel [#old_league_channel] | Sets the league channel"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetChannel([Summary("#Channel")]IChannel newChannel, [Summary("#Channel")]IChannel oldChannel = null) {
            SocketGuildChannel socketGuildChannel = null;
            if (oldChannel != null) {
                socketGuildChannel = (SocketGuildChannel)oldChannel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }
   
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
            }
            else
            {
                lc.League.DiscordInformation.Channel = (SocketGuildChannel) newChannel;
                await _database.UpdateLeague(lc.League);
                await ReplyAsync("League updated.");
            }
            
        }

        [Command("setrole"), Summary("(Admin) !setrole [@Role] [#league_channel] | Sets or delete the league role"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetRole([Summary("@Role")]IRole role = null, [Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }
       
            lc.League.DiscordInformation.LeagueRole = (SocketRole)role;
            await _database.UpdateLeague(lc.League);
            if (role == null) {
                await ReplyAsync("League role deleted.");
            }
            else {
                await ReplyAsync("League role assigned. New Role: " + role.Mention);
            }

            // Assign new role to players
            foreach (var player in lc.League.RegisteredPlayers) {
                if (role == null && lc.League.DiscordInformation.LeagueRole != null) {
                    await player.User.RemoveRoleAsync(lc.League.DiscordInformation.LeagueRole);
                }
                else {
                    if (!player.User.Roles.Contains(lc.League.DiscordInformation.LeagueRole)) {
                        await player.User.AddRoleAsync((SocketRole)role);
                    }

                }

            }
            
        }

        [Command("setmodrole"), Summary("(Admin) !setmodrole [@Role] [#league_channel] | Sets or delete the mod role"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetModRole([Summary("@Role")]IRole role = null, [Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }
          
            lc.League.DiscordInformation.ModeratorRole = (SocketRole)role;
            await _database.UpdateLeague(lc.League);
            if (role == null) {
                await ReplyAsync("Mod role deleted.");
            }
            else {
                await ReplyAsync("Mod role assigned. New Role: " + role.Mention);
            }
            
        }

        [Command("startseason"), Summary("(Admin) !startseason [#league_channel] | Ends the current season and starts a new season"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task StartSeason([Summary("#Channel")]IChannel channel = null) {
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }
          
            IMessageChannel repsondChannel;
            if (lc.League.DiscordInformation.Channel != null)
            {
                repsondChannel = (IMessageChannel) lc.League.DiscordInformation.Channel;
            }
            else
            {
                repsondChannel = (IMessageChannel) socketGuildChannel;
            }
            await lc.StartNewSeason(repsondChannel);
            
        }


        [Command("hey"), Summary("Hey.")]
        public async Task Say()
        {
            await ReplyAsync("Fuck you");
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

		//private int saltCounter;
		[Command("salt"), Summary("How much salt can there be?")]
		public async Task Salt()
		{
			int n = rnd.Next(1, 13);
			await ReplyAsync("There have been " + n + " <:PJSalt:300736349596811265> occurrences today");
		}

		// By Grammis' request )))
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
