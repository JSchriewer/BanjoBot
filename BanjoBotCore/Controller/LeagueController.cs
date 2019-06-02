using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BanjoBotCore.Controller;
using BanjoBotCore.Model;
using Discord;
using Discord.WebSocket;
using log4net;

namespace BanjoBotCore
{

    //TODO: throw Exception in Remove/AddPLayer() <- bool? removePlayerResult = Lobby.RemovePlayer(user);
    //->Duplicated code LeavePlayer / KickPlayer
    //TODO: Don't catch database exceptions
    //->Use try{}finally{} for error handling wherever possible
    // Eventargs should be immutable -> implement ICloneable in Model classes or use DTOs later
    
    public class LeagueController
    {
        private event EventHandler<RegistrationEventArgs> PlayerRegistrationAccepted;
        private event EventHandler<RegistrationEventArgs> PlayerRegistered;
        private event EventHandler<LobbyEventArgs> LobbyClosed;
        private event EventHandler<LobbyEventArgs> LobbyFull;
        private event EventHandler<LobbyEventArgs> LobbyStarted;
        private event EventHandler<LobbyPlayerEventArgs> LobbyCanceled;
        private event EventHandler<LobbyPlayerEventArgs> LobbyHosted;
        private event EventHandler<LobbyPlayerEventArgs> PlayerKicked;
        private event EventHandler<LobbyPlayerEventArgs> PlayerJoined;
        private event EventHandler<LobbyPlayerEventArgs> PlayerLeft;
        private event EventHandler<LobbyPlayerEventArgs> PlayerVotedCancel;
        private event EventHandler<LobbyVoteEventArgs> PlayerVoted;
        private event EventHandler<MatchEventArgs> MatchEnded;


        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(LeagueController));
        public League League;
        private DatabaseController _database;

        public LeagueController(League league)
        {            
            League = league;
            _database = new DatabaseController();
        }
        

        public async Task RegisterEventListener(ILeagueEventListener listener)
        {
            PlayerRegistrationAccepted += listener.PlayerRegistrationAccepted;
            PlayerRegistered += listener.PlayerRegistered;
            LobbyHosted += listener.LobbyCreated;
            LobbyClosed += listener.LobbyClosed;
            LobbyCanceled += listener.LobbyCanceled;
            LobbyFull += listener.LobbyFull;
            PlayerVoted += listener.PlayerVoted;
            PlayerVoted += listener.PlayerKicked;
            PlayerVoted += listener.PlayerJoined;
            PlayerVoted += listener.PlayerLeft;
            PlayerVotedCancel += listener.PlayerVotedCancel;
            MatchEnded += listener.MatchEnded;
            LobbyStarted += listener.LobbyStarted;
            SeasonEnded += listener.SeasonEnded;
        }

        public bool LobbyExists
        {
            get { return League.Lobby != null; }
        }

        private async Task CancelLobby(Player player = null)
        {
            League.Lobby.IsClosed = true;
            await _database.UpdateLobby(League.Lobby);
            foreach (var p in League.Lobby.WaitingList)
            {
                p.CurrentGame = null;
            }
            League.Lobby = null;
            await OnLobbyCanceled(League.Lobby, player);
        }

        public async Task StartGame(Player player)
        {
            if (!LobbyExists)
                throw new  LeagueException("No games open. Type !hostgame to create a game.");

            // If the player who started the game was not the host
            if (League.Lobby.Host != player)
                throw new  LeagueException(player.User.Mention + " only the host (" + League.Lobby.Host.Name + ") can start the game.");

            if (League.Lobby.WaitingList.Count < Lobby.MAXPLAYERS)
                throw new  LeagueException(player.User.Mention + " you need 8 players to start the game.");


            // If the game sucessfully started
            foreach (Player p in League.Lobby.WaitingList)
            {
                p.CurrentGame = League.Lobby;
            }

            try
            {
                Lobby lobby = League.Lobby;
                Match newMatch = await CreateMatch(lobby);
                lobby.StartGame(newMatch);
                await _database.UpdateLobby(lobby);
                League.LobbyInProgress.Add(lobby);
                League.Lobby = null;
                await OnLobbyStarted(lobby);
            }
            catch (Exception e)
            {
                throw e;
            }

         
        }

        private async Task<Match> CreateMatch(Lobby lobby)
        {
            Match newMatch = new Match(lobby);
            newMatch.MatchID = await _database.InsertMatch(newMatch);
           
            return newMatch;
        }

        private async Task CreateLobby(Player host)
        {
            League.Lobby = new Lobby(host, League);
            League.Lobby.LobbyID = await _database.InsertLobby(League.Lobby);
            host.CurrentGame = League.Lobby;
            await OnLobbyHosted(League.Lobby,host);
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
                await AcceptRegistration(player);
            }
            else
            {
                log.Debug("Add applicant" + player.User.Username + " to " + League.Name);
                try
                {
                    await _database.InsertSignupToLeague(player.SteamID, League);
                    League.Applicants.Add(player);
                    await OnPlayerRegistered(player, League);
                }
                catch (Exception e)
                {
                    throw e;
                }
                
            }
        }
        
        public async Task AcceptRegistration(Player player)
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
            await OnPlayerRegistrationAccepted(player);
        }

        public async Task RemovePlayerFromLeague(Player player)
        {
            League.Applicants.Remove(player);
            await _database.DeleteRegistration(player.SteamID, League);
        }

        private async Task SaveMatchResult(Teams pWinnerTeam, Match match)
        {
            if(match == null)
                return;
            if (pWinnerTeam == Teams.None)
                return;

            List<Player> winnerTeam = new List<Player>();
            List<Player> loserTeam = new List<Player>();
            if (pWinnerTeam == Teams.Blue)
            {
                winnerTeam = match.GetTeam(Teams.Blue);
                loserTeam = match.GetTeam(Teams.Red);
            }
            else if (pWinnerTeam == Teams.Red)
            {
                winnerTeam = match.GetTeam(Teams.Red);
                loserTeam = match.GetTeam(Teams.Blue);
            }

            if(pWinnerTeam != Teams.Draw) { 
                int mmrAdjustment = MatchMaker.CalculateMmrAdjustment(winnerTeam, loserTeam, League.LeagueID, League.Season);
                await AdjustPlayerStats(winnerTeam, loserTeam, mmrAdjustment);
            }

            //Saving to database
            try
            {
                // Update Matchresult
                await _database.UpdateMatch(match);
                League.Matches.Add(match);
            }
            catch (Exception e)
            {
                throw e;
            }

            await OnMatchEnded(match);
        }

        public async Task CloseLobby(Lobby lobby, Teams winnerTeam, Match match = null)
        {
            League.LobbyInProgress.Remove(lobby);
            lobby.IsClosed = true;

            if (match == null)
            {
                match = lobby.Match;
                match.Winner = winnerTeam;
                await match.SetMatchResult(winnerTeam);
            }
            
            await _database.UpdateLobby(lobby);

            List<Player> allPlayer = match.GetPlayers();
            foreach (var player in allPlayer)
            {
                player.Matches.Add(match);
            }

            foreach (var player in allPlayer)
                player.CurrentGame = null;
            await SaveMatchResult(winnerTeam, match);         
        }

        public async Task CloseLobbyByEvent(Match matchResult) {
            //Check if match is hosted here on discord
            Lobby lobby = null;
            foreach (var lobbyInProgress in League.LobbyInProgress) {
                if (League.Lobby.Match.MatchID == matchResult.MatchID) {
                    lobby = lobbyInProgress;
                }
            }
            matchResult.StatsRecorded = true;
            if (lobby != null)
            {
                await CloseLobby(lobby, matchResult.Winner, matchResult);
            }                
        }

        public async Task AdjustPlayerStats(List<Player> winnerTeam, List<Player> loserTeam, int mmrAdjustment)
        {
            foreach (var player in winnerTeam)
            {
                player.IncWins(League.LeagueID, League.Season);
                player.IncMMR(League.LeagueID, League.Season,
                        mmrAdjustment + 2 * player.GetLeagueStats(League.LeagueID, League.Season).Streak);
                player.IncStreak(League.LeagueID, League.Season);
                player.IncMatches(League.LeagueID, League.Season);

                try
                {
                    await _database.UpdatePlayerStats(player, player.GetLeagueStats(League.LeagueID, League.Season));
                }
                finally
                {

                }
              
            }

            foreach (var player in loserTeam)
            {
                player.IncLosses(League.LeagueID, League.Season);
                player.SetStreakZero(League.LeagueID, League.Season);
                player.DecMMR(League.LeagueID, League.Season, mmrAdjustment);
                player.IncMatches(League.LeagueID, League.Season);
                if (player.GetLeagueStats(League.LeagueID, League.Season).MMR < 0)
                    player.SetMMR(League.LeagueID, League.Season, 0);

                //TODO: try catch?
                await _database.UpdatePlayerStats(player, player.GetLeagueStats(League.LeagueID, League.Season));
            }        
        }

        public async Task StartNewSeason()
        {
            int season = League.Season;
            
            foreach (Player player in League.RegisteredPlayers)
            {
                PlayerStats newStats = new PlayerStats(League.LeagueID, League.Season + 1);
                player.PlayerStats.Add(newStats);
                await _database.UpdatePlayerStats(player, newStats);
            }

            League.Season++;
            League.Matches = new List<Match>();
            League.GameCounter = 0;
            await _database.UpdateLeague(League);

            await OnSeasonEnded(League, season);
        }
        public async Task CloseLobbyByModerator(int matchID, Teams team) {
            Lobby startedGame = null;
            foreach (var runningGame in League.LobbyInProgress) {
                if (runningGame.Match.MatchID == matchID) {
                    startedGame = runningGame;
                    break;
                }
            }

            if (startedGame == null)
                throw new  LeagueException($"Match #{matchID} not found");

            await CloseLobby(startedGame, team);

        }
        public async Task HostLobby(Player host)
        {
            if (host.IsIngame)
                throw new  LeagueException("Vote before hosting another game");
            if (LobbyExists)
                throw new  LeagueException("Lobby is already open. Only one Lobby may be hosted at a time. \nType !join to join the game.");

            await CreateLobby(host);
        }

        public async Task JoinLobby(Player player)
        {
            if (player.IsIngame)
                throw new  LeagueException("Vote before joining another game");
            if (!LobbyExists)
                throw new  LeagueException("No games open. Type !hostgame to create a game.");

            // Attempt to add player
            bool? addPlayerResult = League.Lobby.AddPlayer(player);

            // If unsuccessfull
            if (addPlayerResult == false)
                throw new  LeagueException(player.User.Mention + " The Lobby is full.");

            // If player already in game
            if (addPlayerResult == null)
                throw new  LeagueException(player.User.Mention + " you can not join a game you are already in.");

            player.CurrentGame = League.Lobby;
            await _database.UpdateLobby(League.Lobby);
            await OnPlayerJoined(League.Lobby, player);
        }

        public async Task LeaveLobby(Player player)
        {
            // If no games are open.
            if (!LobbyExists)
                throw new  LeagueException("No games open. Type !hostgame to create a game.");

            // Attempt to remove player
            bool? removePlayerResult = League.Lobby.RemovePlayer(player);

            // If player not in game
            if (removePlayerResult == null)
               throw new  LeagueException(player.User.Mention + " you are not in this game.");

            await OnPlayerLeft(League.Lobby, player);
            player.CurrentGame = null;
            

            // If game now empty
            if (removePlayerResult == false)
            {
                await CancelLobby();
            }
            else
            {
                await _database.UpdateLobby(League.Lobby);
            }
        }

        public async Task KickPlayer(Player player) {
            // If no games are open.
            if (!LobbyExists) 
                throw new  LeagueException("No games open.");
            

            // Attempt to remove player
            bool? removePlayerResult = League.Lobby.RemovePlayer(player);

            // If player not in game
            if (removePlayerResult == null)
                throw new  LeagueException(player.User.Mention + " is not in the game.");

            player.CurrentGame = null;
            await OnPlayerKicked(League.Lobby, player);

            // If game now empty
            if (removePlayerResult == false)
            {
                await CancelLobby();
            }
            else
            {
                await _database.UpdateLobby(League.Lobby);
            }


        }
        
        public async Task CancelLobbyByCommand(Player player)
        {
            if (!LobbyExists)
                throw new  LeagueException("No games open. Type !hostgame to create a game.");

            if (player == League.Lobby.Host || player.User.Roles.Contains(League.DiscordInformation.ModeratorRole) || player.User.GuildPermissions.Administrator)
                await CancelLobby(player);
            else
                throw new InsufficientPermissionException("No permission");
        }
        
        public async Task VoteCancel(Player player)
        {
            if (League.Lobby == null)
                throw new  LeagueException("No games open. Type !hostgame to create a game.");
           
            if (!League.Lobby.WaitingList.Contains(player)) 
                throw new  LeagueException(player.User.Mention + " only players who were in the game can vote.");

            if (League.Lobby.CancelCalls.Contains(player))
                throw new  LeagueException(player.User.Mention + " you have already voted.");

            League.Lobby.CancelCalls.Add(player);

            if (League.Lobby.CancelCalls.Count >= League.Lobby.GetCancelThreshold())
                await CancelLobby();
        }
        
        public async Task VoteWinner(Player player, Teams team)
        {
            if (!player.IsIngame)
                throw new  LeagueException(player.User.Mention + " you are not in a game.");

            Lobby lobby = player.CurrentGame;
            if (lobby.BlueWinCalls.Contains(player))
            {
                if (team == Teams.Blue)
                {
                    throw new  LeagueException(player.User.Mention + " you have already voted for this team.");
                }
                else
                {
                    lobby.BlueWinCalls.Remove(player);
                  
                }
            }
            else if (lobby.RedWinCalls.Contains(player))
            {
                if (team == Teams.Red) {
                    throw new  LeagueException(player.User.Mention + " you have already voted for this team.");
                }
                else
                {
                    lobby.RedWinCalls.Remove(player);
                }
            }
            else if (lobby.DrawCalls.Contains(player))
            {
                if (team == Teams.Draw)
                {
                    throw new  LeagueException(player.User.Mention + " you have already voted for this team.");
                }
                else
                {
                    lobby.DrawCalls.Remove(player);
                }
            }
      
            switch (team)
            {
                case Teams.Red:
                    lobby.RedWinCalls.Add(player);
                    break;
                case Teams.Blue:
                    lobby.BlueWinCalls.Add(player);
                    break;
                case Teams.Draw:
                    lobby.DrawCalls.Add(player);
                    break;
            }

            Teams winner = Teams.None;
            if (lobby.BlueWinCalls.Count == Lobby.VOTETHRESHOLD)
                winner = Teams.Blue;
            if (lobby.RedWinCalls.Count == Lobby.VOTETHRESHOLD)
                winner = Teams.Red;
            if (lobby.DrawCalls.Count == Lobby.VOTETHRESHOLD) 
                winner = Teams.Draw;

            if(winner != Teams.None)
            {
                await CloseLobby(lobby, winner);
            }
        }        

        public async Task ReCreateLobby(int matchID, IGuildUser playerToRemove)
        {
            Lobby startedGame = null;
            foreach (var runningGame in League.LobbyInProgress) {
                if (runningGame.Match.MatchID == matchID)
                {
                    startedGame = runningGame;
                    break;
                }
            }

            if (startedGame == null)
                throw new  LeagueException("Match not found");

            await CloseLobby(startedGame, Teams.Draw);

            Lobby lobby = null;
            if (!LobbyExists)
            {
                foreach (var player in startedGame.WaitingList)
                {
                    if (playerToRemove != player.User)
                    {
                        await CreateLobby(player);
                        break;
                    }
                }
                
            }

            lobby = League.Lobby;

            string message = "";
            if (League.DiscordInformation.LeagueRole != null)
                message = League.DiscordInformation.LeagueRole.Mention;
            
            if (lobby?.WaitingList.Count == 1)
            {
                foreach (var player in startedGame.WaitingList)
                {
                    if (player.User.Id != playerToRemove.Id && !League.Lobby.WaitingList.Contains(player))
                        League.Lobby.AddPlayer(player);
                }
            }
            else
            {
                throw new  LeagueException("There is already a open Lobby with more than 1 player, please rejoin yourself");
            }
           
            await LobbyChanged();
        }

        private async Task LobbyChanged()
        {
            if (League.Lobby.WaitingList.Count >= Lobby.MAXPLAYERS)
                await OnLobbyFull(League.Lobby);

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

        protected async Task OnPlayerRegistered(Player player, League league)
        {
            EventHandler<RegistrationEventArgs> handler = PlayerRegistered;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;
            args.League = league;

            handler?.Invoke(this, args);
        }
        protected async Task OnPlayerRegistrationAccepted(Player player)
        {
            EventHandler<RegistrationEventArgs> handler = PlayerRegistrationAccepted;
            RegistrationEventArgs args = new RegistrationEventArgs();
            args.Player = player;
            args.League = League;

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyHosted(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = LobbyHosted;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.League = League;
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyClosed(Lobby lobby)
        {
            EventHandler<LobbyEventArgs> handler = LobbyClosed;
            LobbyEventArgs args = new LobbyEventArgs();
            args.League = League;
            args.Lobby = lobby;

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyFull(Lobby lobby)
        {
            EventHandler<LobbyEventArgs> handler = LobbyFull;
            LobbyEventArgs args = new LobbyEventArgs();
            args.League = League;
            args.Lobby = lobby;

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyCanceled(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = LobbyCanceled;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.League = League;
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerJoined(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = PlayerJoined;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.League = League;
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }
        protected async Task OnPlayerLeft(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = PlayerLeft;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.League = League;
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }
        protected async Task OnPlayerKicked(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = PlayerKicked;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.League = League;
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerVoted(Lobby lobby, Player player, Teams team)
        {
            EventHandler<LobbyVoteEventArgs> handler = PlayerVoted;
            LobbyVoteEventArgs args = new LobbyVoteEventArgs();
            args.League = League;
            args.Lobby = lobby;
            args.Player = player;
            args.Team = team;
            
            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerVoted(Lobby lobby, Player player, Teams team, Teams previousVote)
        {
            EventHandler<LobbyVoteEventArgs> handler = PlayerVoted;
            LobbyVoteEventArgs args = new LobbyVoteEventArgs();
            args.League = League;
            args.Lobby = lobby;
            args.Player = player;
            args.Team = team;
            args.prevVote = previousVote;

            handler?.Invoke(this, args);
        }
        protected async Task OnPlayerVotedCancel(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = PlayerVotedCancel;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.Lobby = lobby;
            args.Player = player;
            args.League = League;

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyStarted(Lobby lobby)
        {
            EventHandler<LobbyEventArgs> handler = LobbyStarted;
            LobbyEventArgs args = new LobbyEventArgs();
            args.League = League;
            args.Lobby = lobby;

            handler?.Invoke(this, args);
        }
        protected async Task OnMatchEnded(Match matchResult)
        {
            EventHandler<MatchEventArgs> handler = MatchEnded;
            MatchEventArgs args = new MatchEventArgs();
            args.Match = matchResult;
            args.League = League;

            handler?.Invoke(this, args);
        }

        protected async Task OnSeasonEnded(League league, int season)
        {
            EventHandler<SeasonEventArgs> handler = SeasonEnded;
            SeasonEventArgs args = new SeasonEventArgs();
            args.League = league;
            args.Season = season;
            
            handler?.Invoke(this, args);
        }
    }   

    public class LeagueEventArgs : EventArgs
    {
        public League League { get; set; }
    }

    public class RegistrationEventArgs : LeagueEventArgs
    {
        public Player Player { get; set; }
    }

    public class LobbyEventArgs : LeagueEventArgs
    {
        public Lobby Lobby { get; set; }
    }

    public class LobbyPlayerEventArgs : LobbyEventArgs
    {
        public Player Player { get; set; }
    }

    public class LobbyVoteEventArgs : LobbyPlayerEventArgs
    {
        public Teams Team { get; set; }
        public Teams prevVote { get; set; } = Teams.None;
    }

    public class MatchEventArgs : LeagueEventArgs
    {
        public Match Match { get; set; }
    }

    public class SeasonEventArgs : LeagueEventArgs
    {
        public int Season { get; set; }
    }

    [Serializable]
    public class InsufficientPermissionException : Exception
    {

        public InsufficientPermissionException(string message) : base(message)
        {
        }
    }

    public class LeagueException : Exception
    {
        public LeagueException(string message) : base(message)
        {
        }
    }
}
