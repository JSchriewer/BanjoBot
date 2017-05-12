using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.API;
using Discord.Commands;
using Discord.WebSocket;
using IChannel = Discord.IChannel;

namespace BanjoBot {
    //TODO: try catch throws database exceptions
    //TODO: OnOffline remove from lobby
    //TODO: Refactor: Move everything league related to LeagueController
    //TODO: Refactor: CheckFunctions, e.g. current channel or #parameter
    //TODO: ^Check preconditions
    //TODO: !AddWarning
    public class CommandModule : ModuleBase
    {
        private const string RULE_URL = "https://docs.google.com/document/d/1ibvVJ1o7CSuPl8AfdEJN4j--2ivC93XOKulVq28M_BE";
        private const string STEAM_PROFILE_URL = "https://steamcommunity.com/profiles/";
        private DiscordSocketClient _bot;
        private List<LeagueServer> _allServers;
        private DatabaseController _database;
        private CommandService _commandService;
        public CommandModule(List<LeagueServer> servers, DatabaseController databaseController, DiscordSocketClient bot, CommandService commandService)
        {
            _bot = bot;
            _allServers = servers;
            _database = databaseController;
            _commandService = commandService;
        }

        [Command("hostgame"), Summary("Creates a new game (if there is no open lobby)"), Alias(new string[] { "host", "hg" }), RequireLeaguePermission]
        public async Task Host() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.TryHostGame(Context.Channel, player);
        }

        [Command("join"), Summary("Joins the open game"), Alias(new string[] { "j"}), RequireLeaguePermission]
        public async Task Join() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.JoinGame(Context.Channel, player);  
        }

        [Command("leave"), Summary("Leaves the open game"), Alias(new string[] { "l" }), RequireLeaguePermission]
        public async Task Leave() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.LeaveGame(Context.Channel, player);
        }

        [Command("cancel"), Summary("Cancel the current lobby (only host / moderator)"), Alias(new string[] { "c" }), RequireLeaguePermission]
        public async Task CancelGame() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.CancelLobby(Context.Channel,player);
        }

        [Command("votecancel"), Summary("Casts a vote to cancel the open game"), Alias(new string[] { "vc" }), RequireLeaguePermission]
        public async Task VoteCancel() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.VoteCancel(Context.Channel, player);
        }

        [Command("startgame"), Summary("Start the game. Host only, requires full game"), Alias(new string[] { "sg" }), RequireLeaguePermission]
        public async Task StartGame() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.StartGame(Context.Channel, player);
        }

        [Command("getplayers"), Summary("Shows the players that have joined the open game"), Alias(new string[] { "players","list" }), RequireLeagueChannel]
        public async Task GetPlayers() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            await lc.ListPlayers(Context.Channel);
        }

        [Command("showstats"), Summary("Shows the stats of a player (option: @player)"), Alias(new string[] { "stats", "gs" }), RequireLeaguePermission]
        public async Task ShowStats([Summary("@Player")]IGuildUser guildUser = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = null;
            if (guildUser == null)
            {
                player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            }
            else
            {
                player = lc.League.GetPlayerByDiscordID(guildUser.Id);
            }
            await lc.GetStats(Context.Channel, player);
        }

        [Command("showhistory"), Summary("Shows your match history (option: !history <season #>"), Alias(new string[] { "sh", "history" }), RequireLeaguePermission]
        public async Task ShowStats([Summary("season #")]int season = -1) {
            SocketGuildChannel socketGuildChannel =  (SocketGuildChannel)Context.Channel;

            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);

            if (season != -1) {
                await lc.GetMatchHistory(player,season);
            }
            else {
                await lc.GetMatchHistory(player);
            }
        }

        [Command("mystats"), Summary("Shows your stats with more detailed information"), Alias(new string[] { "mystats", "ms" }), RequireLeaguePermission]
        public async Task ShowMyStats() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);

            await lc.GetFullPlayerStats(player);
        }

        [Command("getgames"), Summary("Shows the status of all games"), Alias(new string[] { "gg", "games" }), RequireLeagueChannel]
        public async Task ShowGames() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            await lc.ShowGames(Context.Channel);
        }


        [Command("bluewins"), Summary("Cast vote for Blue Team as the winner of your current game(post game only)"), Alias(new string[] { "blue", "bw" }), RequireLeaguePermission]
        public async Task VoteBlue() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.VoteWinner(Context.Channel,player,Teams.Blue);
        }

        [Command("redwins"), Summary("Cast vote for Red Team as the winner of your current game (post game only)."), Alias(new string[] { "red", "rw" }), RequireLeaguePermission]
        public async Task VoteRed() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.VoteWinner(Context.Channel, player, Teams.Red);
        }

        [Command("won"), Summary("Cast vote for Red Team as the winner of your current game (post game only)."), Alias(new string[] { "win" }), RequireLeaguePermission]
        public async Task Win() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            if (!player.IsIngame())
            {
                await ReplyAsync("You are not ingame");
                return;
            }

            Teams team = player.CurrentGame.BlueList.Contains(player) ? Teams.Blue : Teams.Red;
            await lc.VoteWinner(Context.Channel, player, team);
        }

        [Command("lost"), Summary("Cast vote for Red Team as the winner of your current game (post game only)."), Alias(new string[] { "loss" }), RequireLeaguePermission]
        public async Task Lost() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            if (!player.IsIngame())
            {
                await ReplyAsync("You are not ingame");
                return;
            }

            Teams team = player.CurrentGame.BlueList.Contains(player) ? Teams.Red : Teams.Blue;
            await lc.VoteWinner(Context.Channel, player, team);
        }

        [Command("draw"), Summary("Cast vote for Red Team as the winner of your current game (post game only)."), RequireLeaguePermission]
        public async Task VoteDraw() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            await lc.VoteWinner(Context.Channel, player, Teams.Draw);
        }

        [Command("topmmr"), Summary("Shows the top 5 players"), Alias(new string[] { "top", "t" }), RequireLeagueChannel]
        public async Task TopMMR() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            await lc.ShowTopMMR(Context.Channel);
        }

        [Command("register"), Summary("League registrations: !register <steamid> <#league-channel>")]
        public async Task Register([Summary("SteamID")]ulong steamid = 0 , [Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);

            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }
            
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            
            Player player = lc.League.GetPlayerByDiscordID(Context.User.Id);
            if (player != null) {
                await ReplyAsync("You are already registered");
                return;
            }

            player = lc.League.GetApplicantByDiscordID(Context.User.Id);
            if (player != null)
            {
                await ReplyAsync("You are already signed up, wait for the approval by a moderator");
                return;
            }

            if (lc.League.NeedSteamToRegister)
            {
                if (steamid == 0) {
                    await ReplyAsync("Missing steamID. Please use !register <YourSteamID64>");
                    return;
                }

                if (!steamid.ToString().StartsWith("7656")) {
                    await ReplyAsync("Thats not a valid steamid64, please follow the instructions in #welcome");
                    return;
                }

                foreach (var regplayer in lc.League.RegisteredPlayers)
                {
                    if (regplayer.SteamID == steamid)
                    {
                        await ReplyAsync("The SteamID is already in use, please contact a moderator");
                        return;
                    }
                }
            }

            player = mms.GetPlayer(Context.User.Id);
            if (player == null) {
                player = new Player((SocketGuildUser)Context.User, steamid);
                await _database.InsertNewPlayer(player);

            }
            if (lc.League.AutoAccept)
            {
                await lc.RegisterPlayer(player);
                await _database.RegisterPlayerToLeague(player, lc.League);
                await _database.UpdatePlayerStats(player, player.GetLeagueStat(lc.League.LeagueID,lc.League.Season));
                await ReplyAsync( player.User.Mention + "You are registered now. Use !help to see the command list");
            }
            else
            {
            
                lc.League.Applicants.Add(player);
                await _database.InsertSignupToLeague(player.SteamID, lc.League);
                await ReplyAsync("You are signed up now. Wait for the approval by a moderator");
                //TODO: add somewhere? modchannel?
            }
        }

        [Command("help"), Summary("Shows all commands"), Alias(new string[] { "h", "?" })]
        public async Task Help()
        {
            String s = "Commands:\n";
            int count = 0;
            foreach (var command in _commandService.Commands)
            {
                count++;
                s += String.Format("{0,-18} {1,-12}\n", command.Name, command.Summary);
                if (count % 15 == 0) {
                    await (await Context.User.CreateDMChannelAsync()).SendMessageAsync("```" + s + "```");
                    count = 0;
                    s = "";
                }
        
            }

            await (await Context.User.CreateDMChannelAsync()).SendMessageAsync("```" + s + "```");
        }

    
        // Moderator commands
        [Command("end"), Summary("Ends a game !end <match-nr #> <team> (moderator)"), RequireLeaguePermission]
        public async Task EndGame([Summary("matchID")]int match, [Summary("team")]Teams team) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!((lc.League.ModeratorRole != null && ((SocketGuildUser)(Context.User)).RoleIds.Contains(lc.League.ModeratorRole.Id)) || ((SocketGuildUser)(Context.User)).GuildPermissions.Administrator)) {
                return;
            }

            await lc.EndGameByModerator(Context.Channel, match,team);
        }

        [Command("recreatelobby"), Summary("Ends a game !end <match-nr #> <team> (moderator)"), Alias(new string[] { "rcl"}), RequireLeaguePermission]
        public async Task ReCreateLobby([Summary("matchID")]int match, [Summary("Player to remove")]IGuildUser player) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!((lc.League.ModeratorRole != null && ((SocketGuildUser)(Context.User)).RoleIds.Contains(lc.League.ModeratorRole.Id)) || ((SocketGuildUser)(Context.User)).GuildPermissions.Administrator)) {
                return;
            }
            
            await lc.ReCreateLobby(Context.Channel,match, player);
        }

        [Command("kick"), Summary("kicks the player from the current lobby. !kick @player (moderator)")]
        public async Task Kick([Summary("@Player")]IGuildUser guildUser) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            LeagueController lc = mms.GetLeagueController((SocketGuildChannel)Context.Channel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!((lc.League.ModeratorRole != null && ((SocketGuildUser)(Context.User)).RoleIds.Contains(lc.League.ModeratorRole.Id)) || ((SocketGuildUser)(Context.User)).GuildPermissions.Administrator)) {
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

        [Command("accept"), Summary("Accepts a applicant. !accept <#league-channel> <@player>  (moderator)")]
        public async Task Accept([Summary("@Player")]IGuildUser guildUser, [Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!((lc.League.ModeratorRole != null && ((SocketGuildUser)(Context.User)).RoleIds.Contains(lc.League.ModeratorRole.Id)) || ((SocketGuildUser)(Context.User)).GuildPermissions.Administrator)) {
                return;
            }

            Player player = null;
            player = lc.League.GetApplicantByDiscordID(guildUser.Id);
            if (player == null) {
                await ReplyAsync("applicant not found");
                return;
            }

            await lc.RegisterPlayer(player);
            await _database.RegisterPlayerToLeague(player, lc.League);
            await _database.UpdatePlayerStats(player, player.GetLeagueStat(lc.League.LeagueID, lc.League.Season));

            await ReplyAsync(player.User.Mention + "You got a private message!");
            await (await (player.User as IGuildUser).CreateDMChannelAsync()).SendMessageAsync("Your registration for " + lc.League.Name + " got approved.\nYou can now start playing!\n\n If you need help, ask a moderator or use !help\n\n Note: Please make sure you read the Rules: \n" + RULE_URL);
        }

        [Command("decline"), Summary("Declines a applicant. !decline <#league-channel> <@player> <reasoning> (moderator)")]
        public async Task Accept([Summary("@Player")]IGuildUser guildUser, string reasoning = "", [Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
                return;
            }

            if (!((lc.League.ModeratorRole != null && ((SocketGuildUser)(Context.User)).RoleIds.Contains(lc.League.ModeratorRole.Id)) || ((SocketGuildUser)(Context.User)).GuildPermissions.Administrator)) {
                return;
            }

            if (channel == null) {
                await ReplyAsync("usage: !accept #channel @player");
                return;
            }

            if (guildUser == null) {
                await ReplyAsync("usage: !accept #channel @player");
                return;
            }

            Player player = null;
            player = lc.League.GetApplicantByDiscordID(guildUser.Id);
            if (player == null) {
                await ReplyAsync("applicant not found");
                return;
            }

            lc.League.Applicants.Remove(player);
            await _database.DeclineRegistration(player.SteamID, lc.League);

            await ReplyAsync(player.User.Mention + "You got a private message!");
            if(!reasoning.Equals(""))
                await (await (player.User as IGuildUser).CreateDMChannelAsync()).SendMessageAsync("Your registration for " + lc.League.Name + " got declined.\n Reason: " +  reasoning +"\nTry again or contact a moderator");
            else
                await (await (player.User as IGuildUser).CreateDMChannelAsync()).SendMessageAsync("Your registration for " + lc.League.Name + " got declined.\nTry again or contact a moderator");
        }

        [Command("listleagues"), Summary("Shows all leagues (moderator)")]
        public async Task ListLeagues() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);

            bool hasPermission = false;
            foreach (var lc in mms.LeagueController) {
                if (((lc.League.ModeratorRole != null && ((SocketGuildUser)(Context.User)).RoleIds.Contains(lc.League.ModeratorRole.Id)) || ((SocketGuildUser)(Context.User)).GuildPermissions.Administrator)) {
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
            foreach (var lc in mms.LeagueController) {
                string channel = lc.League.Channel != null ? lc.League.Channel.Name : "none";
                string role = lc.League.Role != null ? lc.League.Role.Name : "none";
                string modrole = lc.League.ModeratorRole != null ? lc.League.ModeratorRole.Name : "none";
                args = new object[] {lc.League.LeagueID, lc.League.Name,
                    channel, role, lc.League.AutoAccept, lc.League.NeedSteamToRegister,
                    lc.League.Season, lc.League.GameCounter, lc.League.RegisteredPlayers.Count,
                    lc.League.Applicants.Count, modrole};
                s += String.Format("{0,-4} {1,-10} {2,-10} {3,-12} {4,-10} {5,-8} {6,-8} {7,-8} {8,-8} {9,-8} {10,-10}\n", args);
            }
            await ReplyAsync("```" + s + "```");
        }

        [Command("listapplicants"), Summary("Lists all applicants (Moderator)")]
        public async Task ListApplicants() {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            bool hasPermission = false;
            foreach (var lc in mms.LeagueController) {
                if (((lc.League.ModeratorRole != null && ((SocketGuildUser)(Context.User)).RoleIds.Contains(lc.League.ModeratorRole.Id)) || ((SocketGuildUser)(Context.User)).GuildPermissions.Administrator)) {
                    hasPermission = true;
                }
            }
            if (!hasPermission) {
                return;
            }

            object[] args = new object[] { "DiscordID", "Name", "SteamID","Steam Profile", "League" };
            String s = String.Format(
                        "{0,-24} {1,-12} {2,-24} {3,-64} {4,-24}\n", args);
            foreach (var lc in mms.LeagueController) {
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

        [Command("listplayers"), Summary("Lists all applicants (Moderator)")]
        public async Task ListPlayer([Summary("#Channel")]IChannel channel) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);

            bool hasPermission = false;
            foreach (var league in mms.LeagueController) {
                if (((league.League.ModeratorRole != null && ((SocketGuildUser)(Context.User)).RoleIds.Contains(league.League.ModeratorRole.Id)) || ((SocketGuildUser)(Context.User)).GuildPermissions.Administrator)) {
                    hasPermission = true;
                }
            }
            if (!hasPermission) {
                return;
            }

            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)channel;
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("League not found");
                return;
            }

            object[] args = new object[] {"DiscordID", "Name", "SteamID", "Matches", "M+D", "Wins","Losses","Mmr"};
            String s = String.Format(
                "{0,-24} {1,-14} {2,-24} {3,-8} {4,-8} {5,-10} {6,-8} {7,-8}\n", args);


            foreach (var player in lc.League.RegisteredPlayers)
            {
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

        //[Command("setmodchannel"), Summary("Sets moderator channel (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        //public async Task SetAutoAccept([Summary("#Channel")]IChannel channel) {
        //    LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
        //    SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;


        //    lc.League.AutoAccept = autoAccept;
        //    await _database.UpdateLeague(lc.League);
        //    await ReplyAsync("Autoaccept set to " + autoAccept);
        //}

        [Command("autoaccept"), Summary("Sets registration to automatic if true. !autoaccept true/false (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAutoAccept([Summary("True/False")]bool  autoAccept, [Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await ReplyAsync("Join a league channel first or pass a channel as a parameter #channel.");
                return;
            }

            lc.League.AutoAccept = autoAccept;
            await _database.UpdateLeague(lc.League);
            await ReplyAsync("Autoaccept set to " + autoAccept);
        }

        [Command("steamregister"), Summary("Enables steam requirement. !steamregister true/false (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSteamRegister([Summary("True/False")]bool steamregister, [Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)Context.Channel;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }
            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("Join a league channel first or pass a channel as a parameter #channel.");
                return;
            }

            lc.League.NeedSteamToRegister = steamregister;
            await _database.UpdateLeague(lc.League);
        }

        [Command("createleague"), Summary("Creates a league. !createLeague <#Channel> (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task CreateLeague([Summary("LeagueName")]string name, [Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null)
            {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else
            {
                 socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            if (mms.IsGlobal())
            {
                LeagueController lc = mms.GetLeagueController(socketGuildChannel);
                lc.League.Channel = socketGuildChannel;
                lc.League.Name = name;
                await _database.UpdateLeague(lc.League);
                await ReplyAsync("Global league disabled. League assigned to channel " + socketGuildChannel.Name);
            }
            else
            {
                LeagueController lc = mms.GetLeagueController(socketGuildChannel);
                if (lc != null) {
                    await ReplyAsync("This channel is already assigned to another league.");
                }
                else 
                {
                    int leagueID = await _database.InsertNewLeague(Context.Guild.Id);
                    League league = new League(leagueID,name ,1,socketGuildChannel,null);
                    mms.AddLeague(league);
                    await _database.UpdateLeague(league);
                    await ReplyAsync("League created.");
                }
            }
        }

        [Command("deleteleague"), Summary("Deletes a league (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeleteLeague([Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            if (mms.IsGlobal()) {
                await ReplyAsync("There is only a global league. Set a channel for the league with the command setchannel #channel");
                return;
            }

            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc != null)
            {
                await _database.DeleteLeague(lc.League);
                mms.DeleteLeague(lc);
                await ReplyAsync("League deleted.");
            }
            else {
                await ReplyAsync("This channel is not assigned to league.");
            }
            
        }

        [Command("setchannel"), Summary("Sets the league channel (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetChannel([Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            if (mms.IsGlobal()) {
                LeagueController lc = mms.GetLeagueController(socketGuildChannel);
                lc.League.Channel = socketGuildChannel;
                await _database.UpdateLeague(lc.League);
                await ReplyAsync("Global league disabled. League assigned to channel " + socketGuildChannel.Name);
            }
            else {
                LeagueController lc = mms.GetLeagueController(socketGuildChannel);
                if (lc == null) {
                    await ReplyAsync("This is no league channel.");
                }
                else
                {
                    lc.League.Channel = socketGuildChannel;
                    await _database.UpdateLeague(lc.League);
                    await ReplyAsync("League updated.");
                }
            }
        }

        [Command("setrole"), Summary("Sets the league role (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetRole([Summary("@Role")]IRole role = null, [Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
            }
            else {
                lc.League.Role = (SocketRole)role;
                await _database.UpdateLeague(lc.League);
                if (role == null) {
                    await ReplyAsync("League role deleted.");
                }
                else {
                    await ReplyAsync("League role assigned. New Role: " + role.Mention);
                }

                // Assign new role to players
                foreach (var player in lc.League.RegisteredPlayers) {
                    if (role == null && lc.League.Role != null) {
                        await player.User.RemoveRolesAsync(lc.League.Role);
                    }
                    else {
                        if (!player.User.RoleIds.Contains(lc.League.Role.Id)) {
                            await player.User.AddRolesAsync((SocketRole)role);
                        }

                    }

                }
            }
        }

        [Command("setmodrole"), Summary("Sets the mod role (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetModRole([Summary("@Role")]IRole role = null, [Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
            }
            else {
                lc.League.ModeratorRole = (SocketRole)role;
                await _database.UpdateLeague(lc.League);
                if (role == null) {
                    await ReplyAsync("Mod role deleted.");
                }
                else {
                    await ReplyAsync("Mod role assigned. New Role: " + role.Mention);
                }
            }
        }

        [Command("startseason"), Summary("Ends the current season and starts a new season (Admin)"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task StartSeason([Summary("#Channel")]IChannel channel = null) {
            LeagueServer mms = GetMatchMakingServer(Context.Guild.Id);
            SocketGuildChannel socketGuildChannel = null;
            if (channel != null) {
                socketGuildChannel = (SocketGuildChannel)channel;
            }
            else {
                socketGuildChannel = (SocketGuildChannel)Context.Channel;
            }

            LeagueController lc = mms.GetLeagueController(socketGuildChannel);
            if (lc == null) {
                await ReplyAsync("This is no league channel.");
            }
            else
            {
                IMessageChannel repsondChannel;
                if (lc.League.Channel != null)
                {
                    repsondChannel = (IMessageChannel) lc.League.Channel;
                }
                else
                {
                    repsondChannel = (IMessageChannel) socketGuildChannel;
                }
                await lc.StartNewSeason(repsondChannel);
            }
        }


        [Command("hey"), Summary("Hey.")]
        public async Task Say()
        {
            await ReplyAsync("Fuck you");
        }

        [Command("increasemmr"), Summary("increases the mmr of a @player by 25")]
        public async Task increasemmr([Summary("@Player")]IGuildUser guildUser)
        {
            await ReplyAsync("ERROR User not found, resetting all mmr");
        }

        public LeagueServer GetMatchMakingServer(ulong serverid) {
            foreach (LeagueServer myServer in _allServers) {
                if (myServer.DiscordServer.Id == serverid)
                    return myServer;
            }

            return null;
        }
    }

    public class RequireLeaguePermission : PreconditionAttribute {
        public override async Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IDependencyMap map) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)context.Channel;
            List<LeagueServer> servers = map.Get<List<LeagueServer>>();
            foreach (var matchMakingServer in servers) {
                if (matchMakingServer.DiscordServer.Id == context.Guild.Id) {
                    LeagueController lc = matchMakingServer.GetLeagueController(socketGuildChannel);
                    if (lc == null) {
                        await context.Channel.SendMessageAsync("Join a league channel and try again");
                        return PreconditionResult.FromError("Join a league channel and try again");
                    }

                    foreach (var player in lc.League.RegisteredPlayers) {
                        if (player.User.Id == context.User.Id) {
                            return PreconditionResult.FromSuccess();
                        }
                    }
                    await context.Channel.SendMessageAsync("You are not registered. Sign up with !register <YourSteamID64> <#league-channel>");
                    return PreconditionResult.FromError("You are not registered. Sign up with !register <YourSteamID64> <#league-channel>");
                }
            }
            return PreconditionResult.FromError("Error: Server not found");
        }
    }


    public class RequireLeagueChannel : PreconditionAttribute {

        public override async Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IDependencyMap map) {
            SocketGuildChannel socketGuildChannel = (SocketGuildChannel)context.Channel;
            List<LeagueServer> servers = map.Get<List<LeagueServer>>();
            foreach (var matchMakingServer in servers) {
                if (matchMakingServer.DiscordServer.Id == context.Guild.Id) {
                    LeagueController lc = matchMakingServer.GetLeagueController(socketGuildChannel);
                    if (lc == null)
                    {
                        await context.Channel.SendMessageAsync("This is not a league channel. Join a league channel first or use !register <steamid64> <#channel>");
                        return PreconditionResult.FromError("Join a league channel and try again");
                    }
                    else {
                        return PreconditionResult.FromSuccess();
                    }
                }
            }
            return PreconditionResult.FromError("Error: Server not found");
        }
    }

}
