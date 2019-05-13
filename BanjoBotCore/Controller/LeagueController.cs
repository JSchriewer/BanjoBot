using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BanjoBotCore.Controller;
using Discord;
using Discord.WebSocket;
using log4net;

namespace BanjoBotCore
{

    //TODO: throw Exception in Remove/AddPLayer() <- bool? removePlayerResult = Lobby.RemovePlayer(user);
    //->Duplicated code LeavePlayer / KickPlayer
    //TODO: Catch Database Exceptions
    
    public class LeagueController
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(LeagueController));
        public League League;
        public Lobby Lobby { get; set; }
        public List<Lobby> GamesInProgress { get; set; }
        private DatabaseController _database;

        public LeagueController(League league)
        {
            GamesInProgress = new List<Lobby>();
            League = league;
            _database = new DatabaseController();
        }
        

        public async Task RegisterEventListener(ILeagueEventListener listener)
        {
            LobbyCreated += listener.LobbyCreated;
            LobbyClosed += listener.LobbyClosed;
            LobbyChanged += listener.LobbyChanged;
            MatchEnded += listener.MatchEnded;
            PlayerAdded += listener.AddedPlayerToLeague;
        }

        public bool LobbyExists
        {
            get { return Lobby != null; }
        }

        private async Task CancelLobby()
        {
            Lobby = null;
            await OnLobbyClosed();
        }

        public async Task StartGame(Player player)
        {
            if (!LobbyExists)
                throw new Exception("No games open. Type !hostgame to create a game.");

            // If the player who started the game was not the host
            if (Lobby.Host != player)
                throw new Exception(player.User.Mention + " only the host (" + Lobby.Host.Name + ") can start the game.");

            if (Lobby.WaitingList.Count < 8)
                throw new Exception(player.User.Mention + " you need 8 players to start the game.");


            // If the game sucessfully started
            foreach (Player p in Lobby.WaitingList)
            {
                p.CurrentGame = Lobby;
            }

            try
            {
                int match_id = await _database.InsertMatch(League.LeagueID, League.Season, Lobby.BlueList, Lobby.RedList);
                Lobby.MatchID = match_id;
                Lobby.StartGame();
                GamesInProgress.Add(Lobby);
                Lobby = null;
            }
            catch (Exception e)
            {
                throw e;
            }
         
        }

        private async Task<Lobby> CreateLobby(Player host)
        {
            Lobby game = new Lobby(host, League);
            Lobby = game;
            return game;
        }

        public async Task<Player> RegisterPlayer(SocketGuildUser user, ulong steamID)
        {
            log.Debug("Creating new player");
            Player player = new Player(user, steamID);
            try
            {
                await _database.InsertPlayer(player);
            
            }
            catch (Exception e)
            {
                throw e;
            }

            await RegisterPlayer(player);
            return player;
        }

        public async Task RegisterPlayer(Player player)
        {
            if (League.DiscordInformation.AutoAccept)
            {
                await AddPlayerToLeague(player);
            }
            else
            {
                log.Debug("Add applicant" + player.User.Username + " to " + League.Name);
                try
                {
                    await _database.InsertSignupToLeague(player.SteamID, League);
                    League.Applicants.Add(player);
                    await OnApplicantAdded(player, League);
                }
                catch (Exception e)
                {
                    throw e;
                }
                
            }
        }
        
        public async Task AddPlayerToLeague(Player player)
        {
            log.Debug("RegisterPlayer: " + player.Name + "(" + player.User.Id + ")");

            try
            {
                player.PlayerStats.Add(new PlayerStats(League.LeagueID, League.Season));
                await _database.InsertRegistrationToLeague(player, League);
                await _database.UpdatePlayerStats(player, player.GetLeagueStats(League.LeagueID, League.Season));
            }
            catch(Exception e)
            {
                throw e;
            }

            if (League.Applicants.Contains(player))
            {
                League.Applicants.Remove(player);
            }

            League.RegisteredPlayers.Add(player);
            await OnPlayerAdded(player);
        }

        public async Task RemovePlayer(Player player)
        {
            League.Applicants.Remove(player);
            await _database.DeleteRegistration(player.SteamID, League);
        }
        private async Task SaveMatchResult(Teams winnerTeam, MatchResult match)
        {
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
            int mmrAdjustment = await MatchMaker.CalculateMmrAdjustment(winner, looser, League.LeagueID, League.Season);

            //Adding Details to Match-Object
            match.Date = DateTime.Now;

            foreach (var stats in match.PlayerMatchStats)
            {
                Player player = League.RegisteredPlayers.Find(p => p.SteamID == stats.SteamID);

                stats.Match = match;
                if (stats.Team == winnerTeam)
                {
                    stats.MmrAdjustment = mmrAdjustment;
                    stats.StreakBonus = 2 * player.GetLeagueStats(League.LeagueID, League.Season).Streak;
                    stats.Win = true;
                }
                else {
                    stats.MmrAdjustment = -mmrAdjustment;
                    stats.StreakBonus = 0;
                    stats.Win = false;
                }
            }

            List<Player> allPlayer = new List<Player>();
            allPlayer.AddRange(winner);
            allPlayer.AddRange(looser);

            //Saving Data
            try
            {
                await _database.UpdateMatchResult(match);
                League.Matches.Add(match);
                await AdjustPlayerStats(winner, looser);
                foreach (var player in allPlayer)
                {
                    await _database.UpdatePlayerStats(player, player.GetLeagueStats(League.LeagueID, League.Season));
                    player.Matches.Add(match);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                foreach (var player in allPlayer)
                    player.CurrentGame = null;
            }
        

            await OnMatchEnded(match);

        }

        public async Task CloseLobbyByCommand(Lobby lobby, Teams winnerTeam, MatchResult match = null)
        {
            GamesInProgress.Remove(lobby);
            lobby.Winner = winnerTeam;
            
            if (lobby.StartMessage != null) {
                await lobby.StartMessage.UnpinAsync();
            }

            if (winnerTeam == Teams.Draw) {
                await DrawMatch(lobby);
                return;
            }

            if (match == null)
                match = new MatchResult(lobby);

           
            await SaveMatchResult(winnerTeam, match);
           
         
        }

        //League logic does not apply on public games 
        //if(lobby != null) why is even there?
        public async Task CloseLobbyByEvent(MatchResult matchResult) {
            Lobby lobby = null;
            foreach (var game in GamesInProgress) {
                if (game.MatchID == matchResult.MatchID) {
                    lobby = game;
                }
            }
            matchResult.StatsRecorded = true;
            if (lobby != null)
            {
                await CloseLobbyByCommand(lobby, matchResult.Winner, matchResult);
            }
            else
            {                
                await SaveMatchResult(matchResult.Winner, matchResult);
            }
                
        }

        public async Task DrawMatch(Lobby game)
        {
            foreach (var player in game.WaitingList)
            {
                player.CurrentGame = null;
            }
            await _database.DrawMatch(game.MatchID);
        }

        public async Task AdjustPlayerStats(List<Player> winner, List<Player> looser)
        {
            int mmrAdjustment = await MatchMaker.CalculateMmrAdjustment(winner, looser, League.LeagueID, League.Season);

            foreach (var user in winner)
            {
                user.IncWins(League.LeagueID, League.Season);
                user.IncMMR(League.LeagueID, League.Season,
                        mmrAdjustment + 2 * user.GetLeagueStats(League.LeagueID, League.Season).Streak);
                user.IncStreak(League.LeagueID, League.Season);
                user.IncMatches(League.LeagueID, League.Season);
            }

            foreach (var user in looser)
            {
                user.IncLosses(League.LeagueID, League.Season);
                user.SetStreakZero(League.LeagueID, League.Season);
                user.DecMMR(League.LeagueID, League.Season, mmrAdjustment);
                user.IncMatches(League.LeagueID, League.Season);
                if (user.GetLeagueStats(League.LeagueID, League.Season).MMR < 0)
                    user.SetMMR(League.LeagueID, League.Season, 0);
            }
        }

        public async Task StartNewSeason()
        {

            foreach(Player player in League.RegisteredPlayers)
            {
                PlayerStats newStats = new PlayerStats(League.LeagueID, League.Season + 1);
                player.PlayerStats.Add(newStats);
                await _database.UpdatePlayerStats(player, newStats);
            }
            
            League.Season++;
            League.Matches = new List<MatchResult>();
            League.GameCounter = 0;
            await _database.UpdateLeague(League);

        }

        public async Task CloseLobbyByModerator(int matchID, Teams team) {
            Lobby startedGame = null;
            foreach (var runningGame in GamesInProgress) {
                if (runningGame.MatchID == matchID) {
                    startedGame = runningGame;
                    break;
                }
            }

            if (startedGame == null)
                throw new Exception("Vote before hosting another game");

            await CloseLobbyByCommand(startedGame, team);

        }
        public async Task HostLobby(Player host)
        {
            if (host.IsIngame)
                throw new Exception("Vote before hosting another game");
            if (LobbyExists)
                throw new Exception("Lobby is already open. Only one Lobby may be hosted at a time. \nType !join to join the game.");

            Lobby newGame = await CreateLobby(host);
            await OnLobbyCreated();
        }

        public async Task JoinLobby(Player player)
        {
            if (player.IsIngame)
                throw new Exception("Vote before joining another game");
            if (!LobbyExists)
                throw new Exception("No games open. Type !hostgame to create a game.");

            // Attempt to add player
            bool? addPlayerResult = Lobby.AddPlayer(player);

            // If unsuccessfull
            if (addPlayerResult == false)
                throw new Exception(player.User.Mention + " The Lobby is full.");

            // If player already in game
            if (addPlayerResult == null)
                throw new Exception(player.User.Mention + " you can not join a game you are already in.");

            await OnLobbyChanged();
        }

        public async Task LeaveLobby(Player player)
        {
            // If no games are open.
            if (!LobbyExists)
                throw new Exception("No games open. Type !hostgame to create a game.");

            // Attempt to remove player
            bool? removePlayerResult = Lobby.RemovePlayer(player);

            // If player not in game
            if (removePlayerResult == null)
               throw new Exception(player.User.Mention + " you are not in this game.");
            
            // If game now empty
            if(removePlayerResult == false)
                await CancelLobby();

            if(removePlayerResult == true)
                await OnLobbyChanged();

        }

        public async Task KickPlayer(Player player) {
            // If no games are open.
            if (!LobbyExists) 
                throw new Exception("No games open.");
            

            // Attempt to remove player
            bool? removePlayerResult = Lobby.RemovePlayer(player);

            // If player not in game
            if (removePlayerResult == null)
                throw new Exception(player.User.Mention + " is not in the game.");

            // If game now empty
            else if (removePlayerResult == false) {
                await CancelLobby();
            }

            if (removePlayerResult == true)
                await OnLobbyChanged();
        }
        
        public async Task CancelLobby(Player player)
        {
            if (!LobbyExists)
                throw new Exception("No games open. Type !hostgame to create a game.");

            if (player == Lobby.Host || player.User.Roles.Contains(League.DiscordInformation.ModeratorRole) || player.User.GuildPermissions.Administrator)
                await CancelLobby();
            else
                throw new InsufficientPermissionException("No permission");
        }
        
        public async Task VoteCancel(Player player)
        {
            if (Lobby == null)
                throw new Exception("No games open. Type !hostgame to create a game.");
           
            if (!Lobby.WaitingList.Contains(player)) 
                throw new Exception(player.User.Mention + " only players who were in the game can vote.");

            if (Lobby.CancelCalls.Contains(player))
                throw new Exception(player.User.Mention + " you have already voted.");

            Lobby.CancelCalls.Add(player);

            if (Lobby.CancelCalls.Count >= Math.Ceiling((double) Lobby.WaitingList.Count()/2))
                await CancelLobby();
        }
        
        public async Task VoteWinner(Player player, Teams team)
        {
            if (!player.IsIngame)
                throw new Exception(player.User.Mention + " you are not in a game.");

            Lobby game = player.CurrentGame;
            if (game.BlueWinCalls.Contains(player))
            {
                if (team == Teams.Blue || team == Teams.Draw)
                {
                    throw new Exception(player.User.Mention + " you have already voted for this team.");
                }
                else if (team == Teams.Red)
                {
                    game.BlueWinCalls.Remove(player);
                    game.RedWinCalls.Add(player);
                  
                }
            }
            else if (game.RedWinCalls.Contains(player))
            {
                if (team == Teams.Red || team == Teams.Draw) {
                    throw new Exception(player.User.Mention + " you have already voted for this team.");
                }
                else if (team == Teams.Blue) {
                    game.RedWinCalls.Remove(player);
                    game.BlueWinCalls.Add(player);
                   
                }
            }
            else if (game.DrawCalls.Contains(player))
            {
                throw new Exception(player.User.Mention + " you have already voted for this team.");
            }
            else
            {
                switch (team)
                {
                    case Teams.Red:
                        game.RedWinCalls.Add(player);
                        break;
                    case Teams.Blue:
                        game.BlueWinCalls.Add(player);
                        break;
                    case Teams.Draw:
                        game.DrawCalls.Add(player);
                        break;
                }
            }

            Teams winner = Teams.None;
            if (game.BlueWinCalls.Count == Lobby.VOTETHRESHOLD)
                winner = Teams.Blue;
            if (game.RedWinCalls.Count == Lobby.VOTETHRESHOLD)
                winner = Teams.Red;
            if (game.DrawCalls.Count == Lobby.VOTETHRESHOLD) 
                winner = Teams.Draw;

            if(winner != Teams.None)
            {
                await CloseLobbyByCommand(game, winner);
            }
        }        

        public async Task ReCreateLobby(int matchID, IGuildUser playerToRemove)
        {
            Lobby startedGame = null;
            foreach (var runningGame in GamesInProgress) {
                if (runningGame.MatchID == matchID)
                {
                    startedGame = runningGame;
                    break;
                }
            }

            if (startedGame == null)
                throw new Exception("Match not found");

            await CloseLobbyByCommand(startedGame, Teams.Draw);

            Lobby lobby = null;
            if (!LobbyExists)
            {
                foreach (var player in startedGame.WaitingList)
                {
                    if (playerToRemove != player.User)
                    {
                        lobby = await CreateLobby(player);
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
            }
           
            await OnLobbyChanged();
        }

        public async Task SetModChannel(IChannel modChannel)
        {
            League.DiscordInformation.ModeratorChannel = (SocketGuildChannel)modChannel;
            await _database.UpdateLeague(League);
        }
        public async Task SetAutoAccept(Boolean autoAccept)
        {
            League.DiscordInformation.AutoAccept = autoAccept;
            await _database.UpdateLeague(League);
        }

        public async Task SetSteamRegister(Boolean steamRegister)
        {
            League.DiscordInformation.NeedSteamToRegister = steamRegister;
            await _database.UpdateLeague(League);
        }
        public async Task SetChannel(SocketGuildChannel socketGuildChannel)
        {
            League.DiscordInformation.Channel = (SocketGuildChannel)socketGuildChannel;
            await _database.UpdateLeague(League);
        }

        public async Task SetLeagueRole(SocketRole role)
        {
            League.DiscordInformation.LeagueRole = role;
            await _database.UpdateLeague(League);
        }

        public async Task SetModRole(SocketRole role)
        {
            League.DiscordInformation.ModeratorRole = role;
            await _database.UpdateLeague(League);
        }

        protected async Task OnLobbyClosed()
        {
            EventHandler<LeagueEventArgs> handler = LobbyClosed;
            LeagueEventArgs args = new LeagueEventArgs();
            args.League = League;
            args.Lobby = Lobby;
            args.GamesInProgress = GamesInProgress;

            if (handler != null)
            {
                handler(this, args);
            }
        }

        protected async Task OnLobbyCreated()
        {
            await OnLobbyChanged();
            EventHandler<LeagueEventArgs> handler = LobbyCreated;
            LeagueEventArgs args = new LeagueEventArgs();
            args.League = League;
            args.Lobby = Lobby;
            args.GamesInProgress = GamesInProgress;

            if (handler != null)
            {
                handler(this, args);
            }
        }

        protected async Task OnLobbyChanged()
        {
            EventHandler<LeagueEventArgs> handler = LobbyChanged;
            LeagueEventArgs args = new LeagueEventArgs();
            args.League = League;
            args.Lobby = Lobby;
            args.GamesInProgress = GamesInProgress;

            if (handler != null)
            {
                handler(this, args);
            }
        }
        protected async Task OnMatchEnded(MatchResult matchResult)
        {
            EventHandler<MatchEventArgs> handler = MatchEnded;
            MatchEventArgs args = new MatchEventArgs();
            args.MatchResult = matchResult;

            if (handler != null)
            {
                handler(this, args);
            }
        }
        protected async Task OnApplicantAdded(Player player, League league)
        {
            EventHandler<RegistrationEventArgs> handler = ApplicantAdded;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;
            args.League = League;

            if (handler != null)
            {
                handler(this, args);
            }
        }
        protected async Task OnPlayerAdded(Player player)
        {
            EventHandler<RegistrationEventArgs> handler = PlayerAdded;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;

            if (handler != null)
            {
                handler(this, args);
            }
        }

        public event EventHandler<RegistrationEventArgs> PlayerAdded;
        public event EventHandler<RegistrationEventArgs> ApplicantAdded;
        public event EventHandler<LeagueEventArgs> LobbyClosed;
        public event EventHandler<LeagueEventArgs> LobbyCreated;
        public event EventHandler<LeagueEventArgs> LobbyChanged;
        public event EventHandler<MatchEventArgs> MatchEnded;
    }

    public class RegistrationEventArgs : EventArgs
    {
        public Player Player { get; set; }
        public League League { get; set; }
    }

    public class LeagueEventArgs : EventArgs
    {
        public League League { get; set; }
        public Lobby Lobby { get; set; }
        public List<Lobby> GamesInProgress { get; set; }
    }

    public class MatchEventArgs : EventArgs
    {
        public MatchResult MatchResult { get; set; }
    }

    [Serializable]
    public class InsufficientPermissionException : Exception
    {
        public InsufficientPermissionException()
        {
        }

        public InsufficientPermissionException(string message) : base(message)
        {
        }

        public InsufficientPermissionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InsufficientPermissionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
