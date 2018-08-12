using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Threading.Tasks;
using BanjoBot.model;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace BanjoBot
{
    public class LeagueController
    {

        //TODO: Refactor: Split discord related stuff & controlling
        //TODO: Refactor: Entrypoint for controlling: checkPreconditions() -> Controlling -> SendResultString ->  Controlling
        //^ not its preconditions in start command, some controlling in there "Lobby=null"
        public League League;
        private Lobby Lobby { get; set; }
        public List<Lobby> RunningGames { get; set; }
        private DatabaseController _database;

        public LeagueController(League league)
        {
            RunningGames = new List<Lobby>();
            League = league;
            _database = new DatabaseController();
        }

        private bool LobbyExists()
        {
            return Lobby != null;
        }

        private async Task CancelLobby()
        {
            Lobby = null;
            await UpdateChannelWithLobby();
        }

        private async Task StartGame()
        {
            foreach (Player player in Lobby.WaitingList)
            {
                player.CurrentGame = Lobby;
            }
            Lobby.GameNumber = ++League.GameCounter;
            Lobby.StartGame();
            RunningGames.Add(Lobby);
            await UpdateChannelWithLobby();
            int match_id = await _database.InsertNewMatch(League.LeagueID,League.Season,Lobby.BlueList,Lobby.RedList);
            Lobby.MatchID = match_id;
        }

        private async Task<Lobby> HostGame(Player host)
        {
            Lobby game = new Lobby(host, League);
            Lobby = game;
            return game;
        }

        public async Task RegisterPlayer(Player player)
        {
            if (League.RegisteredPlayers.Contains(player))
            {
                return;
            }

            if (League.Applicants.Contains(player))
            {
                League.Applicants.Remove(player);
            }

            player.PlayerStats.Add(new PlayerStats(League.LeagueID, League.Season));
            League.RegisteredPlayers.Add(player);
            await _database.RegisterPlayerToLeague(player, League);
            await _database.UpdatePlayerStats(player, player.GetLeagueStat(League.LeagueID, League.Season));
            await AddDiscordRole(player);
        }

        public async Task AddDiscordRole(Player player)
        {
            if (League.HasDiscord() && League.DiscordInformation.LeagueRole != null) {
                if (!player.User.Roles.Contains(League.DiscordInformation.LeagueRole)) {
                    await player.User.AddRoleAsync(League.DiscordInformation.LeagueRole);
                }
            }
        }


        private async Task CloseGame(Teams winnerTeam, MatchResult match)
        {
            //Adding missing details
            match.Date = DateTime.Now;

            List<Player> winner = new List<Player>();
            List<Player> looser = new List<Player>();
            foreach (var stats in match.PlayerMatchStats)
            {
                Player player = League.RegisteredPlayers.Find(p => p.SteamID == stats.SteamID);
                if (stats.Win)
                    winner.Add(player);
                else
                    looser.Add(player);
            }
            int mmrAdjustment = MatchMaker.CalculateMmrAdjustment(winner, looser, League.LeagueID, League.Season);

            foreach (var stats in match.PlayerMatchStats)
            {
                Player player = League.RegisteredPlayers.Find(p => p.SteamID == stats.SteamID);
                player.CurrentGame = null;

                stats.Match = match;
                if (stats.Team == winnerTeam)
                {
                    stats.MmrAdjustment = mmrAdjustment;
                    stats.StreakBonus = 2* player.GetLeagueStat(League.LeagueID, League.Season).Streak;
                    stats.Win = true;
                }
                else {
                    stats.MmrAdjustment = -mmrAdjustment;
                    stats.StreakBonus = 0;
                    stats.Win = false;
                }

                //TODO: players should have the match instead of the stats
                player.Matches.Add(stats);
            }
            

            await _database.UpdateMatchResult(match); 
            League.Matches.Add(match);

            AdjustPlayerStats(winner, looser);
            List<Player> allPlayer = new List<Player>();
            allPlayer.AddRange(winner);
            allPlayer.AddRange(looser);
            foreach (var player in allPlayer) {
                await _database.UpdatePlayerStats(player, player.GetLeagueStat(League.LeagueID, League.Season)); 
            }
         
        }

        public async Task CloseDiscordGame(Lobby game, Teams winnerTeam, MatchResult match = null)
        {
            RunningGames.Remove(game); 
            game.Winner = winnerTeam;

            await UpdateChannelWithLobby();
            if (game.StartMessage != null) {
                await game.StartMessage.UnpinAsync();
            }

            if (winnerTeam == Teams.Draw) {
                await DrawMatch(game);
                return;
            }

            if (match == null)
                match = new MatchResult(game);

            await CloseGame(winnerTeam, match);

            List<Player> winner = new List<Player>();
            List<Player> looser = new List<Player>();
            foreach (var player in game.WaitingList)
            {
                if ((game.RedList.Contains(player) && winnerTeam == Teams.Red) || (game.BlueList.Contains(player) && winnerTeam == Teams.Blue))
                    winner.Add(player);
                else
                    looser.Add(player);
            }
            int mmrAdjustment = MatchMaker.CalculateMmrAdjustment(winner, looser, League.LeagueID, League.Season);

            printGameResult(game, mmrAdjustment,winnerTeam, (ITextChannel)League.DiscordInformation.Channel);
        }


        public async Task CloseGameByEvent(MatchResult matchResult) {
            //TODO: check teams here again (does it matter if they get forced into teams?)
            Lobby lobby = null;
            foreach (var game in RunningGames) {
                if (game.MatchID == matchResult.MatchID) {
                    lobby = game;
                }
            }
            matchResult.StatsRecorded = true;
            if (lobby != null)
                await CloseDiscordGame(lobby, matchResult.Winner, matchResult);
            else
                await CloseGame(matchResult.Winner, matchResult);
        }

        public async Task DrawMatch(Lobby game)
        {
            foreach (var player in game.WaitingList)
            {
                player.CurrentGame = null;
            }
            await _database.DrawMatch(game.MatchID);
            await SendMessage((ITextChannel)League.DiscordInformation.Channel, "Game " + game.GetGameName() + " has ended in a draw. No stats have been recorded.");
        }

        public void AdjustPlayerStats(List<Player> winner, List<Player> looser)
        {
            int mmrAdjustment = MatchMaker.CalculateMmrAdjustment(winner, looser, League.LeagueID, League.Season);

            foreach (var user in winner)
            {
                user.IncWins(League.LeagueID, League.Season);
                user.IncMMR(League.LeagueID, League.Season,
                        mmrAdjustment + 2*user.GetLeagueStat(League.LeagueID, League.Season).Streak);
                user.IncStreak(League.LeagueID, League.Season);
                user.IncMatches(League.LeagueID, League.Season);
            }

            foreach (var user in looser)
            {
                user.IncLosses(League.LeagueID, League.Season);
                user.SetStreakZero(League.LeagueID, League.Season);
                user.DecMMR(League.LeagueID, League.Season, mmrAdjustment);
                user.IncMatches(League.LeagueID, League.Season);
                if (user.GetLeagueStat(League.LeagueID, League.Season).MMR < 0)
                    user.SetMMR(League.LeagueID, League.Season, 0);
            }
        }

        public async Task EndGameByModerator(IMessageChannel textChannel, int matchID, Teams team) {
            Lobby startedGame = null;
            foreach (var runningGame in RunningGames) {
                if (runningGame.MatchID == matchID) {
                    startedGame = runningGame;
                    break;
                }
            }

            if (startedGame == null) {
                await SendMessage(textChannel, "Match not found");
                return;
            }


            await CloseDiscordGame(startedGame, team);

        }

        private async void printGameResult(Lobby game, int mmr, Teams winnerTeam, ITextChannel textChannel) {
            string message = "Closing lobby\n";
            //TODO: +-24

            switch (winnerTeam) {
                case Teams.Red:
                    message += "Red team has won " + game.GetGameName() + "!\n";
                    message += GetGameResultString(game, mmr, Teams.Red);
                    break;
                case Teams.Blue:
                    message += "Blue team has won " + game.GetGameName() + "!\n";
                    message += GetGameResultString(game, mmr, Teams.Blue);

                    break;
                case Teams.Draw:
                    message += "Game " + game.GetGameName() + " has ended in a draw. No stats have been recorded.";
                    break;
            }

            await SendMessage(textChannel, message);

        }

        private String GetGameResultString(Lobby game, int mmr, Teams winner) {
            char blueSign = '+';
            char redSign = '+';
            if (Teams.Blue == winner)
                redSign = '-';
            else
                blueSign = '-';

            String message = "";
            message += "Blue team (" + blueSign + mmr + "): ";
            foreach (var player in game.BlueList) {
                if (player.GetLeagueStat(League.LeagueID, League.Season).Streak > 1)
                    message += player.PlayerMMRString(League.LeagueID, League.Season) + "+" + 2 * (player.GetLeagueStat(League.LeagueID, League.Season).Streak - 1) + " ";
                else
                    message += player.PlayerMMRString(League.LeagueID, League.Season) + " ";
            }
            message += "\n";
            message += "Red team (" + redSign + mmr + "): ";
            foreach (var player in game.RedList) {
                if (player.GetLeagueStat(League.LeagueID, League.Season).Streak > 1)
                    message += player.PlayerMMRString(League.LeagueID, League.Season) + "+" + 2 * (player.GetLeagueStat(League.LeagueID, League.Season).Streak - 1) + " ";
                else
                    message += player.PlayerMMRString(League.LeagueID, League.Season) + " ";
            }

            return message;
        }

        /// <summary>
        /// Creates a new Game, binding the host and broadcasting to the Channel.
        /// </summary>
        /// <param name="textChannel">The Channel to be broadcasted to.</param>
        /// <param name="host">The User who hosted the game.</param>
        public async Task CreateLobby(IMessageChannel textChannel, Player host)
        {
            if (host.IsIngame())
            {
                await SendTempMessage(textChannel, host.User.Mention + " Vote before hosting another game");
                return;
            }

            // If no games are open.
            if (!LobbyExists())
            {
                Lobby newGame = await HostGame(host);
                await UpdateChannelWithLobby();
                string mention = "";
                if (League.DiscordInformation.LeagueRole != null)
                    mention = League.DiscordInformation.LeagueRole.Mention;
                await SendMessage(textChannel,mention + " New Lobby created by " + host.PlayerMMRString(League.LeagueID, League.Season) + ". \nType !join to join the game. (" + newGame.WaitingList.Count() + "/8)");
            } else { 
                await SendTempMessage(textChannel, host.User.Mention + " Lobby is already open. Only one Lobby may be hosted at a time. \nType !join to join the game.");
            }
        }

        /// <summary>
        /// Adds a User to the currently active Game (if one exists) and broadcasts to the SocketGuildChannel.
        /// </summary>
        /// <param name="textChannel">The SocketGuildChannel to be broadcasted to.</param>
        /// <param name="player">User who wishes to join.</param>
        public async Task JoinLobby(IMessageChannel textChannel, Player player)
        {
            if (player.IsIngame())
            {
                await SendTempMessage(textChannel, player.User.Mention + " Vote before joining another game");
                return;
            } 

            if (!LobbyExists())
            {
                await SendTempMessage(textChannel, "No games open. Type !hostgame to create a game.");
                return;
            }

            // Attempt to add player
            bool? addPlayerResult = Lobby.AddPlayer(player);

            // If successful
            if (addPlayerResult == true)
            {
                await UpdateChannelWithLobby();
                await SendMessage(textChannel,player.PlayerMMRString(League.LeagueID, League.Season) + " has joined the lobby. (" + Lobby.WaitingList.Count() + "/8)");
                if (Lobby.WaitingList.Count() == 8) { 
                    await SendMessage(textChannel,Lobby.Host.User.Mention + ", The lobby is full. Type !startgame to start the game");
                    await SendPrivateMessage(Lobby.Host.User as IGuildUser,"The lobby is full. Type !startgame to start the game");
                }
            }

            // If unsuccessfull
            else if (addPlayerResult == false)
                await SendTempMessage(textChannel, player.User.Mention + " The Lobby is full.");
            // If player already in game
            else if (addPlayerResult == null)
                await SendTempMessage(textChannel, player.User.Mention + " you can not join a game you are already in.");
            else
                await SendTempMessage(textChannel, "Error: Command.joinGame()");
        }

        /// <summary>
        /// Removes a User from the currently active Game (if one exists) and broadcasts to the Channel.
        /// </summary>
        /// <param name="textChannel">The Channel to be broadcasted to.</param>
        /// <param name="user">User who wishes to leave.</param>
        public async Task LeaveLobby(IMessageChannel textChannel, Player user)
        {
            // If no games are open.
            if (!LobbyExists())
            {
                await SendTempMessage(textChannel, "No games open. Type !hostgame to create a game.");
                return;
            }

            // Attempt to remove player
            bool? removePlayerResult = Lobby.RemovePlayer(user);

            // If successful
            if (removePlayerResult == true)
            {
                await SendMessage(textChannel,user.PlayerMMRString(League.LeagueID, League.Season) +" has left the lobby. (" + Lobby.WaitingList.Count() + "/8)");
                await UpdateChannelWithLobby();
            }
            // If game now empty
            else if (removePlayerResult == false)
            {
                await SendMessage(textChannel,user.PlayerMMRString(League.LeagueID, League.Season) + " has left the lobby. (" + Lobby.WaitingList.Count() + "/8)");
                await SendMessage(textChannel,"Closing Lobby");
                await CancelLobby();
            }
            // If player not in game
            else if (removePlayerResult == null)
                await SendTempMessage(textChannel, user.User.Mention + " you are not in this game.");
            else
                await SendTempMessage(textChannel, "Error: Command.leaveGame()");
        }

        /// <summary>
        /// Kicks a player
        /// </summary>
        public async Task KickPlayer(IMessageChannel textChannel, Player user) {
            // If no games are open.
            if (!LobbyExists()) {
                await SendTempMessage(textChannel, "No games open.");
                return;
            }

            // Attempt to remove player
            bool? removePlayerResult = Lobby.RemovePlayer(user);

            // If successful
            if (removePlayerResult == true) {
                await UpdateChannelWithLobby();
                await SendMessage(textChannel,user.PlayerMMRString(League.LeagueID, League.Season) + " got kicked from the lobby. (" + Lobby.WaitingList.Count() + "/8)");
            }
            // If game now empty
            else if (removePlayerResult == false) {
                await SendMessage(textChannel,user.PlayerMMRString(League.LeagueID, League.Season) + " got kicked got kicked from the lobby. (" + Lobby.WaitingList.Count() + "/8)");
                await SendMessage(textChannel,"Closing lobby");
                await CancelLobby();
            }
            // If player not in game
            else if (removePlayerResult == null)
                await SendTempMessage(textChannel, user.User.Mention + " is not in the game.");
            else
                await SendTempMessage(textChannel, "Error: Command.leaveGame()");
        }

        /// <summary>
        /// Starts the currently active game. Only the host can use this command sucessfully.
        /// </summary>
        /// <param name="textChannel">The Channel to be broadcasted to.</param>
        /// <param name="playerUser who wishes to leave.</param>
        public async Task StartGame(IMessageChannel textChannel, Player player)
        {
            if (!LobbyExists())
            {
                await SendTempMessage(textChannel, "No games open. Type !hostgame to create a game.");
                return;
            }

            // If the player who started the game was not the host
            if (Lobby.Host != player) { 
                await SendTempMessage(textChannel, player.User.Mention + " only the host (" + Lobby.Host.User.Username + ") can start the game.");
                return;
            }else if (Lobby.WaitingList.Count < 8) { 
                await SendTempMessage(textChannel, player.User.Mention + " you need 8 players to start the game.");
                return;
            }

            // If the game sucessfully started
            await StartGame();
            String startmessage = Lobby.GetGameName() + "("+ Lobby.MatchID +")" + " has been started by " + player.PlayerMMRString(League.LeagueID,League.Season) + ".";
            // Prepare Blue Team
            String blueTeam = "Blue Team (" + Lobby.GetTeamMMR(Teams.Blue) + "): ";
            foreach (var p in Lobby.BlueList)
            {
                blueTeam += p.User.Mention + "(" + p.GetLeagueStat(League.LeagueID, League.Season).MMR + ") ";   
            }

            // Prepare Red Team
            String redTeam = "Red Team (" + Lobby.GetTeamMMR(Teams.Red) + "): ";
            foreach (var p in Lobby.RedList)
            {
                redTeam += p.User.Mention + "(" + p.GetLeagueStat(League.LeagueID, League.Season).MMR + ") ";
            }

            // Broadcast teams and password
            Lobby.StartMessage = await SendMessage(textChannel,startmessage + "\n" + blueTeam + "\n" + redTeam + "\nPassword: " + Lobby.GeneratePassword(6));
            await Lobby.StartMessage.PinAsync();

            foreach (var p in Lobby.WaitingList)
            {
                await SendPrivateMessage(p.User as IGuildUser,Lobby.GetGameName() +  " has been started");
            }

            Lobby = null;
            await UpdateChannelWithLobby();
        }

        /// <summary>
        /// Cancels the currently active game. Only the host can use this.
        /// </summary>
        /// <param name="textChannel">Channel to broadcast to.</param>
        /// <param name="player">User who called the function.</param>
        public async Task CancelLobby(IMessageChannel textChannel, Player player)
        {
            if (!LobbyExists())
            {
                await SendTempMessage(textChannel,"No games open. Type !hostgame to create a game.");
                return;
            }
             
            //TODO: refactor
            if (player == Lobby.Host || player.User.Roles.Contains(League.DiscordInformation.ModeratorRole) || player.User.GuildPermissions.Administrator)
            {
                await SendMessage(textChannel,"Game canceled by host " + player.User.Username + ".");     
                await CancelLobby();
            } 
        }

        /// <summary>
        /// Records the vote and anmnounces a winner if one is determined.
        /// </summary>
        /// <param name="player">User who voted.</param>
        /// <param name="team">Team that the player voted for.</param>
        public async Task VoteCancel(IMessageChannel textChannel, Player player)
        {
            if (Lobby == null)
            {
                await SendTempMessage(textChannel, "No games open. Type !hostgame to create a game.");
                return;
            }

            if (!Lobby.WaitingList.Contains(player)) { 
                await SendTempMessage(textChannel, player.User.Mention + " only players who were in the game can vote.");
                return;
            }
            if (Lobby.CancelCalls.Contains(player)) { 
                await SendTempMessage(textChannel, player.User.Mention + " you have already voted.");
                return;
            }

            Lobby.CancelCalls.Add(player);
            if (Lobby.CancelCalls.Count >= Math.Ceiling((double) Lobby.WaitingList.Count()/2))
            {
                await SendMessage(textChannel,"Lobby got canceled canceled by vote.");
                await CancelLobby();
            }
            else
            {
                await SendMessage(textChannel,"Vote recorded to cancel game by " + player.User.Username + " (" + Lobby.CancelCalls.Count() + "/" + Math.Ceiling((double)Lobby.WaitingList.Count() / 2) + ")");
            }   
        }

        /// <summary>
        /// Lists all of the players in the currently ActiveGame
        /// </summary>
        /// <param name="textChannel">Channel to broadcast to.</param>
        public async Task ShowLobby(IMessageChannel textChannel)
        {
            if (Lobby == null)
            {
                await SendTempMessage(textChannel, "No games open. Type !hostgame to create a game.");
                return;
            }

            String message = "Lobby (" + Lobby.WaitingList.Count() + "/8)  player: \n";
            foreach (var user in Lobby.WaitingList)
            {
                message += user.PlayerMMRString(League.LeagueID,League.Season) + " ";
            }
            await SendTempMessage(textChannel, message);
        }

        /// <summary>
        /// Records the vote and anmnounces a winner if one is determined.
        /// </summary>
        /// <param name="e">MessageEventArgs, used to extract gameName and Channel</param>
        /// <param name="player">User who voted.</param>
        /// <param name="team">Team that the player voted for.</param>
        public async Task VoteWinner(IMessageChannel textChannel, Player player, Teams team)
        {
            if (!player.IsIngame())
            {
                await SendTempMessage(textChannel, player.User.Mention + " you are not in a game.");
                return;
            }

            Lobby game = player.CurrentGame;
            if (game.BlueWinCalls.Contains(player))
            {
                if (team == Teams.Blue || team == Teams.Draw)
                {
                    await SendTempMessage(textChannel, player.User.Mention + " you have already voted for this team.");
                }
                else if (team == Teams.Red)
                {
                    game.BlueWinCalls.Remove(player);
                    game.RedWinCalls.Add(player);
                    await SendTempMessage(textChannel, player.User.Mention + " has changed his Mind");
                    await SendMessage(textChannel,"Vote recorded for Red team in game " + game.GetGameName() + " by " + player.User.Username + ". (" + game.RedWinCalls.Count() + "/5)");
                }
            }
            else if (game.RedWinCalls.Contains(player))
            {
                if (team == Teams.Red || team == Teams.Draw) {
                    await SendTempMessage(textChannel, player.User.Mention + " you have already voted for this team.");
                }
                else if (team == Teams.Blue) {
                    game.RedWinCalls.Remove(player);
                    game.BlueWinCalls.Add(player);
                    await SendTempMessage(textChannel, player.User.Mention + " has changed his Mind");
                    await SendMessage(textChannel,"Vote recorded for Blue team in game " + game.GetGameName() + " by " + player.User.Username + ". (" + game.BlueWinCalls.Count() + "/5)");
                }
            }
            else if (game.DrawCalls.Contains(player))
            {
                await SendTempMessage(textChannel, player.User.Mention + " you have already voted");
            }
            else
            {
                switch (team)
                {
                    case Teams.Red:
                        game.RedWinCalls.Add(player);
                        await SendMessage(textChannel,"Vote recorded for Red team in game " + game.GetGameName() + " by " + player.User.Username + ". (" + game.RedWinCalls.Count() + "/5)");
                        break;
                    case Teams.Blue:
                        game.BlueWinCalls.Add(player);
                        await SendMessage(textChannel,"Vote recorded for Blue team in game " + game.GetGameName() + " by " + player.User.Username + ". (" + game.BlueWinCalls.Count() + "/5)");
                        break;
                    case Teams.Draw:
                        game.DrawCalls.Add(player);
                        await SendMessage(textChannel,"Vote recorded for draw " + game.GetGameName() + " by " + player.User.Username + ". (" + game.DrawCalls.Count() + "/5)");
                        break;
                }
            }

            if (game.BlueWinCalls.Count == Lobby.VOTETHRESHOLD)
            {
                await CloseDiscordGame(game, Teams.Blue);
            }
            if (game.RedWinCalls.Count == Lobby.VOTETHRESHOLD) {
                await CloseDiscordGame(game, Teams.Red);
            }
            if (game.DrawCalls.Count == Lobby.VOTETHRESHOLD) {
                await CloseDiscordGame(game, Teams.Draw);
            }
        }

        /// <summary>
        /// Prints a list of all the games currently open/in progress.
        /// </summary>
        /// <param name="textChannel">Channel to send the message to.</param>
        public async Task ShowGames(IMessageChannel textChannel)
        {
            if (Lobby != null)
                await SendTempMessage(textChannel, "Open lobby: (" + Lobby.WaitingList.Count() + "/8)");
            else
                await SendTempMessage(textChannel, "No games in lobby.");

            if (RunningGames.Count > 0)
            {
                String message = "Games in progress: ";
                foreach (var game in RunningGames)
                {
                    message += game.GetGameName() + "("+ game.MatchID +"), ";
                }
                await SendTempMessage(textChannel, message);
            }
            else
                await SendTempMessage(textChannel, "No games in progress.");
        }

        /// <summary>
        /// Prints a users stats to the text channel.
        /// </summary>
        /// <param name="textChannel">Channel to send message to.</param>
        /// <param name="user">Users whos stats will be displayed.</param>
        public async Task ShowStats(IMessageChannel textChannel, Player player, int season)
        {
            if (season <= 0)
                season = League.Season;

            PlayerStats stats = player.GetLeagueStat(League.LeagueID, season);
            if (stats == null)
            {
                await SendTempMessage(textChannel, "No stats found");
                return;
            }

            int wins = stats.Wins;
            int losses = stats.Losses;
            int gamesPlayed = wins + losses;
            await SendTempMessage(textChannel, player.PlayerMMRString(League.LeagueID, season) + " has " + gamesPlayed + " games played, " + wins + " wins, " + losses + " losses.\nCurrent win streak: " + stats.Streak + ".");
         

        }

        public async Task ShowPlayerProfile(Player  player, int season)
        {
            float goals = 0;
            float assist = 0;
            float steals = 0;
            float turnovers = 0;
            float st = 0;
            float pickups = 0;
            float passes = 0;
            float pr = 0;
            float save = 0;
            float points = 0;
            float post = 0;
            float tag = 0;
            int statsRecorded = 0;

            List<PlayerMatchStats> seasonMatchStats = player.GetMatchesBySeason(League.LeagueID, season);
            if (seasonMatchStats.Count == 0)
            {
                await SendPrivateMessage(player.User as IGuildUser,"No stats found");
                return;
            }
            
            foreach (var matchStats in seasonMatchStats) {
                if (matchStats.Match.StatsRecorded) {
                    statsRecorded++;
                    goals += matchStats.Goals;
                    assist += matchStats.Assist;
                    steals += matchStats.Steals;
                    turnovers += matchStats.Turnovers;
                    st += matchStats.StealTurnDif;
                    pickups += matchStats.Pickups;
                    passes += matchStats.Passes;
                    pr += matchStats.PassesReceived;
                    save += matchStats.SaveRate;
                    points += matchStats.Points;
                    post += matchStats.PossessionTime;
                    tag += matchStats.TimeAsGoalie;
                }
            }
            PlayerStats playerStats = player.GetLeagueStat(League.LeagueID, season);

            int statCount = statsRecorded;
            // prevents null division
            if (statsRecorded == 0)
                statsRecorded = 1;

            string message = $"**{player.User.Username}'s Profile**\n`";
            message += $"{"League",-24} {League.Name,-12} \n";
            message += $"{"Season",-24} {season,-12} \n";
            message += $"{"Matches",-24} {playerStats.MatchCount,-12} \n";
            message += $"{"Wins",-24} {playerStats.Wins,-12} \n";
            message += $"{"Losses",-24} {playerStats.Losses,-12} \n";
            message += $"{"Winrate",-24} {(float)playerStats.Wins/(float)playerStats.MatchCount,-12:P} \n";
            message += $"{"Streak",-24} {playerStats.Streak,-12} \n";
            message += $"{"Rating",-24} {playerStats.MMR,-12} `\n";
            message += "\n";
            message += $"**AverageStats**\n`";
            message += $"{"Goals",-24} {goals/statsRecorded,-12:N} \n";
            message += $"{"Assist",-24} {assist/statsRecorded,-12:N} \n";
            message += $"{"Steals",-24} {steals/statsRecorded,-12:N} \n";
            message += $"{"Turnovers",-24} {turnovers/statsRecorded,-12:N} \n";
            message += $"{"S-T",-24} {st/statsRecorded,-12:N} \n";
            message += $"{"Pickups",-24} {pickups/statsRecorded,-12:N} \n";
            message += $"{"Passes",-24} {passes/statsRecorded,-12:N} \n";
            message += $"{"PR",-24} {pr/statsRecorded,-12:N} \n";
            message += $"{"SaveRate",-24} {save/statsRecorded,-12:P} \n";
            message += $"{"Points",-24} {points/statsRecorded,-12:N} \n";
            message += $"{"PosT",-24} {post/statsRecorded,-12:N} \n";
            message += $"{"TAG",-24} {tag/statsRecorded,-12:N} \n`";
            message += $"\n*Stats for {statCount} of {playerStats.MatchCount} games were recorded*";

            await SendPrivateMessage(player.User as IGuildUser,message);
        }

        public async Task GetMatchHistory(Player player, int season)
        {
            object[] args = new object[] {"Date", "MatchID", "Goals", "Assist", "Steals", "Turnovers", "S/T", "Pickups", "Passes", "PR", "Save", "Points", "PosT", "TAG", "Mmr", "Streak","Stats","Hero" };
            String s = String.Format("{0,-12} {1,-8} {17,-10} {2,-8} {3,-8} {4,-8} {5,-10} {6,-8} {7,-8} {8,-8} {9,-8} {10,-8} {11,-8} {12,-8} {13,-8} {14,-8} {15,-8} {16,-8}\n", args);
            List<PlayerMatchStats> allStats = player.GetMatchesBySeason(League.LeagueID, season);
            IOrderedEnumerable<PlayerMatchStats> orderedStats = allStats.OrderByDescending(stats => stats.Match.Date);
            for(int i = 0; i < 10 && i < allStats.Count; i++)
            {
                PlayerMatchStats stats = orderedStats.ElementAt(i);
                args = new object[] { DateTime.Parse(stats.Match.Date.ToString()).ToShortDateString(),stats.Match.MatchID,stats.Goals, stats.Assist, stats.Steals, stats.Turnovers, stats.StealTurnDif, stats.Pickups, stats.Passes, stats.PassesReceived, stats.SaveRate, stats.Points,stats.PossessionTime,stats.TimeAsGoalie,stats.MmrAdjustment,stats.StreakBonus,stats.Match.StatsRecorded,stats.HeroID};
                s += String.Format("{0,-12} {1,-8} {17,-10} {2,-8} {3,-8} {4,-8} {5,-10} {6,-8} {7,-8} {8,-8} {9,-8} {10,-8:P0} {11,-8} {12,-8} {13,-8} {14,-8} {15,-8} {16,-8}\n", args);
            }
            if (!orderedStats.Any())
            {
                await SendPrivateMessage(player.User as IGuildUser,"You have not played in season " + season);
            }
            else
            {
                await SendPrivateMessage(player.User as IGuildUser,"```" + s + "```");
            }
            

        }

        public async Task GetMatchHistory(Player player)
        {
            await GetMatchHistory(player, League.Season);

        }

        public async Task ShowPlayerProfile(Player player)
        {
            await ShowPlayerProfile(player, League.Season);
        }

        /// <summary>
        /// Broadcasts the top 5 players and their MMR to the text channel.
        /// </summary>
        /// <param name="textChannel">Channel to broadcast to</param>
        /// <param name="ds">Database object</param>
        public async Task ShowTopMMR(IMessageChannel textChannel)
        {
            // Sort dictionary by MMR
            var sortedDict = League.RegisteredPlayers.OrderByDescending(
                entry => entry.GetLeagueStat(League.LeagueID, League.Season).MMR).ThenBy(entry => entry.GetLeagueStat(League.LeagueID, League.Season).MatchCount).ToList();

            string message = "Top 5 players by MMR: \n";
            int i = 0;
            foreach (Player player in sortedDict)
            {
                if (i == 5)
                    break;
                message += "#" + (i + 1) + " " + player.PlayerMMRString(League.LeagueID,League.Season) + ", ";
                i++;
            }
            await SendTempMessage(textChannel, message);
        }

        public async Task ReCreateLobby(IMessageChannel textChannel, int matchID, IGuildUser playerToRemove)
        {
            Lobby startedGame = null;
            foreach (var runningGame in RunningGames) {
                if (runningGame.MatchID == matchID)
                {
                    startedGame = runningGame;
                    break;
                }
            }

            if (startedGame == null)
            {
                await SendMessage(textChannel,"Match not found");
                return;
            }

            await CloseDiscordGame(startedGame, Teams.Draw);

            Lobby lobby = null;
            if (!LobbyExists())
            {
                foreach (var player in startedGame.WaitingList)
                {
                    if (playerToRemove != player.User)
                    {
                        lobby = await HostGame(player);
                        break;
                    }
                }
                
            }
            else
            {
                lobby = Lobby;
            }

            string message = "";
            if (League.DiscordInformation.LeagueRole != null)
                message = League.DiscordInformation.LeagueRole.Mention;

            if (lobby?.WaitingList.Count == 1)
            {
                foreach (var player in startedGame.WaitingList)
                {
                    if (player.User.Id != playerToRemove.Id && !Lobby.WaitingList.Contains(player))
                        Lobby.AddPlayer(player);
                }
                message += " Game BBL#" + startedGame.GameNumber + " ended in a Draw!\n Lobby got recreated  (" +
                           Lobby.WaitingList.Count() + "/8)";
            }
            else
            {
                message += " Game BBL#" + startedGame.GameNumber + " ended in a Draw!\n There is already a open Lobby with more than 1 player, please rejoin yourself";
            }

            await SendMessage(textChannel, message);
            await UpdateChannelWithLobby();
        }
       
        public async Task StartNewSeason(IMessageChannel textChannel)
        {
            String message = "";
            Player mostActive = null;
            int max = Int32.MinValue;
            foreach (var player in League.RegisteredPlayers)
            {
                if (player.GetLeagueStat(League.LeagueID, League.Season).MatchCount > max)
                {
                    mostActive = player;
                    max = player.GetLeagueStat(League.LeagueID, League.Season).MatchCount;
                }
            }

            string mentionMostActive = mostActive == null ? "fuck you" : mostActive.User.Username;
            message = "**Season " + League.Season + " has ended.**\n";
            message += "Big thanks to our most active player " + mentionMostActive + " with " + max + " matches \n\n";
            message += "**Top Players Season " + League.Season + ": **\n";
            var sortedDict = from entry in League.RegisteredPlayers orderby entry.GetLeagueStat(League.LeagueID, League.Season).MMR descending select entry;

            object[] args = new object[] { "Name", "MMR", "Matches", "Wins", "Losses" };
            string topPlayers = String.Format("{0,-10} {1,-10} {2,-10} {3,-10} {4,-10}\n", args);
            for (int i = 0; i < sortedDict.Count(); i++) {
                if (i < 10) {
                    PlayerStats stats = sortedDict.ElementAt(i).GetLeagueStat(League.LeagueID, League.Season);
                    string username = sortedDict.ElementAt(i).User.Username.Length > 8
                        ? sortedDict.ElementAt(i).User.Username.Substring(0, 8)
                        : sortedDict.ElementAt(i).User.Username;
                    args = new object[] { username, stats.MMR, stats.MatchCount, stats.Wins, stats.Losses };
                    topPlayers += String.Format("{0,-10} {1,-10} {2,-10} {3,-10} {4,-10}\n", args);
                }

                PlayerStats newStats = new PlayerStats(League.LeagueID, League.Season + 1);
                sortedDict.ElementAt(i).PlayerStats.Add(newStats);
                await _database.UpdatePlayerStats(sortedDict.ElementAt(i), newStats);
            }
            string mention = "";
            if (League.DiscordInformation.LeagueRole != null)
                mention = League.DiscordInformation.LeagueRole.Mention + " ";

            await SendMessage(textChannel,mention + message + "```" + topPlayers + "```");
            League.Season++;
            League.Matches = new List<MatchResult>();
            League.GameCounter = 0;
            await _database.UpdateLeague(League);
        
        }

        private async Task UpdateChannelWithLobby() {
            if (!League.HasDiscord())
                return;

            SocketTextChannel channel = (SocketTextChannel)League.DiscordInformation.Channel;
            string topic = "";
            if (Lobby == null)
                topic = "Games in progress: " + RunningGames.Count;
            else
                topic = "Open Lobby (" + Lobby.WaitingList.Count + "/8)" + "    Games in progress: " + RunningGames.Count;

            await channel.ModifyAsync(channelProperties => channelProperties.Topic = topic);
        }

        private async Task SendPrivateMessage(IGuildUser user, String message) {
            if(League.HasDiscord())
                await (await user.GetOrCreateDMChannelAsync()).SendMessageAsync(message);

        }

        private async Task<IUserMessage> SendMessage(IMessageChannel textChannel, String message) {
            if (League.HasDiscord())
                return await textChannel.SendMessageAsync(message);

            return null;
        }

        private async Task SendTempMessage(IMessageChannel textChannel, String message)
        {
            // Send message
            if (!League.HasDiscord())
                return;

            IUserMessage discordMessage = await textChannel.SendMessageAsync(message);
            
            // Delete message
            System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                DeleteMessage(discordMessage);
            }, null);
        }

        private void DeleteMessage(IUserMessage message)
        {
            System.Threading.Thread.Sleep(20 * 1000);
            message.DeleteAsync();
        }
    }
}
