using BanjoBotCore.Model;
using Discord;
using log4net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BanjoBotCore.Controller
{
    public class LobbyController
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(LeagueController));

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

        public List<Lobby> StartedLobbies { get; }
        public Lobby OpenLobby { get; set; }

        private DatabaseController _database;

        public LobbyController()
        {
            StartedLobbies = new List<Lobby>();
            _database = new DatabaseController();
        }

        public async Task RegisterEventListener(ILeagueEventListener listener)
        {
            LobbyHosted += listener.LobbyCreated;
            LobbyClosed += listener.LobbyClosed;
            LobbyCanceled += listener.LobbyCanceled;
            LobbyFull += listener.LobbyFull;
            PlayerVoted += listener.PlayerVoted;
            PlayerKicked += listener.PlayerKicked;
            PlayerJoined += listener.PlayerJoined;
            PlayerLeft += listener.PlayerLeft;
            PlayerVotedCancel += listener.PlayerVotedCancel;
            MatchEnded += listener.MatchEnded;
            LobbyStarted += listener.LobbyStarted;
        }

        public bool LobbyExists
        {
            get { return OpenLobby != null; }
        }

        private async Task LobbyChanged()
        {
            if (OpenLobby.WaitingList.Count >= Lobby.MAXPLAYERS)
                await OnLobbyFull(OpenLobby);

        }

        private async Task CancelLobby(Player player = null)
        {
            OpenLobby.IsClosed = true;
            await _database.UpdateLobby(OpenLobby);
            foreach (var p in OpenLobby.WaitingList)
            {
                p.CurrentGame = null;
            }
            
            await OnLobbyCanceled(OpenLobby, player);
            OpenLobby = null;
        }

        private async Task<Match> CreateMatch(Lobby lobby)
        {
            Match newMatch = new Match(lobby);
            newMatch.MatchID = await _database.InsertMatch(newMatch);

            return newMatch;
        }

        private async Task CreateLobby(Player host, League league)
        {
            OpenLobby = new Lobby(host, league);
            OpenLobby.LobbyID = await _database.InsertLobby(OpenLobby);
            host.CurrentGame = OpenLobby;
            await OnLobbyHosted(OpenLobby, host);
        }

        private async Task SaveMatchResult(Teams winnerTeam, Match match)
        {
            if (winnerTeam == Teams.None)
                return;

            int mmrTeam1 = match.GetTeamMMR(Teams.Blue);
            int mmrTeam2 = match.GetTeamMMR(Teams.Red);
            if (winnerTeam != Teams.Draw)
            {
                int mmrAdjustment = MatchMaker.CalculateMmrAdjustment(mmrTeam1, mmrTeam2, match.League.LeagueID, match.League.Season);
                await match.SetMatchResult(winnerTeam, mmrAdjustment);
                await AdjustPlayerStats(match, mmrAdjustment);
            }
            
            try
            {
                await _database.UpdateMatch(match);
                match.League.Matches.Add(match);
            }
            catch (Exception e)
            {
                throw e;
            }

            await OnMatchEnded(match);
        }

        private async Task AdjustPlayerStats(Match match, int mmrAdjustment)
        {
            if (match.Winner == Teams.None || match.Winner == Teams.Draw)
            {
                log.Debug("Can't adjust player stats if the match is still in progress or drawn (Winner = Team.None/Draw)");
                return;
            }

            foreach (var player in match.GetWinnerTeam())
            {
                player.AdjustStats(match.League, true, mmrAdjustment);
                await _database.UpdatePlayerStats(player, player.GetLeagueStats(match.LeagueID, match.Season));
            
            }

            foreach (var player in match.GetLoserTeam())
            {
                player.AdjustStats(match.League, false, mmrAdjustment);
                await _database.UpdatePlayerStats(player, player.GetLeagueStats(match.LeagueID, match.Season));
            }
        }

        public async Task StartGame(Player player)
        {
            if (!LobbyExists)
                throw new LeagueException("No games open. Type !hostgame to create a game.");

            // If the player who started the game was not the host
            if (OpenLobby.Host != player)
                throw new LeagueException(player.User.Mention + " only the host (" + OpenLobby.Host.Name + ") can start the game.");

            if (OpenLobby.WaitingList.Count < Lobby.MAXPLAYERS)
                throw new LeagueException(player.User.Mention + " you need 8 players to start the game.");


            // If the game sucessfully started
            foreach (Player p in OpenLobby.WaitingList)
            {
                p.CurrentGame = OpenLobby;
            }

            try
            {
                Lobby lobby = OpenLobby;
                Match newMatch = await CreateMatch(lobby);
                lobby.StartGame(newMatch);
                await _database.UpdateLobby(lobby);
                StartedLobbies.Add(lobby);
                OpenLobby = null;
                await OnLobbyStarted(lobby);
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public async Task CloseLobby(Lobby lobby, Teams winnerTeam)
        {
            StartedLobbies.Remove(lobby);
            lobby.IsClosed = true;

            await _database.UpdateLobby(lobby);

            Match match = lobby.Match;
            List<Player> allPlayer = match.GetPlayers();
            foreach (var player in allPlayer)
            {
                player.Matches.Add(match);
            }

            foreach (var player in allPlayer)
                player.CurrentGame = null;
            await SaveMatchResult(winnerTeam, match);
        }

        public async Task CancelLobbyByCommand(Player player)
        {
            if (!LobbyExists)
                throw new LeagueException("No games open. Type !hostgame to create a game.");
            
            await CancelLobby(player);
        }

        public async Task VoteCancel(Player player)
        {
            if (OpenLobby == null)
                throw new LeagueException("No games open. Type !hostgame to create a game.");

            if (!OpenLobby.WaitingList.Contains(player))
                throw new LeagueException(player.User.Mention + " only players who were in the game can vote.");

            if (OpenLobby.CancelCalls.Contains(player))
                throw new LeagueException(player.User.Mention + " you have already voted.");

            OpenLobby.CancelCalls.Add(player);

            if (OpenLobby.CancelCalls.Count >= OpenLobby.GetCancelThreshold())
                await CancelLobby();
        }

        public async Task HostLobby(Player host, League league)
        {
            if (host.IsIngame)
                throw new LeagueException("Vote before hosting another game");
            if (LobbyExists)
                throw new LeagueException("Lobby is already open. Only one Lobby may be hosted at a time. \nType !join to join the game.");

            await CreateLobby(host, league);
        }

        public async Task JoinLobby(Player player)
        {
            if (player.IsIngame)
                throw new LeagueException("Vote before joining another game");
            if (!LobbyExists)
                throw new LeagueException("No games open. Type !hostgame to create a game.");

            // Attempt to add player
            bool? addPlayerResult = OpenLobby.AddPlayer(player);

            // If unsuccessfull
            if (addPlayerResult == false)
                throw new LeagueException(player.User.Mention + " The Lobby is full.");

            // If player already in game
            if (addPlayerResult == null)
                throw new LeagueException(player.User.Mention + " you can not join a game you are already in.");

            player.CurrentGame = OpenLobby;
            await _database.UpdateLobby(OpenLobby);
            await OnPlayerJoined(OpenLobby, player);
        }

        public async Task LeaveLobby(Player player)
        {
            // If no games are open.
            if (!LobbyExists)
                throw new LeagueException("No games open. Type !hostgame to create a game.");

            // Attempt to remove player
            bool? removePlayerResult = OpenLobby.RemovePlayer(player);

            // If player not in game
            if (removePlayerResult == null)
                throw new LeagueException(player.User.Mention + " you are not in this game.");

            await OnPlayerLeft(OpenLobby, player);
            player.CurrentGame = null;


            // If game now empty
            if (removePlayerResult == false)
            {
                await CancelLobby();
            }
            else
            {
                await _database.UpdateLobby(OpenLobby);
            }
        }

        public async Task KickPlayer(Player player)
        {
            // If no games are open.
            if (!LobbyExists)
                throw new LeagueException("No games open.");


            // Attempt to remove player
            bool? removePlayerResult = OpenLobby.RemovePlayer(player);

            // If player not in game
            if (removePlayerResult == null)
                throw new LeagueException(player.User.Mention + " is not in the game.");

            player.CurrentGame = null;
            await OnPlayerKicked(OpenLobby, player);

            // If game now empty
            if (removePlayerResult == false)
            {
                await CancelLobby();
            }
            else
            {
                await _database.UpdateLobby(OpenLobby);
            }


        }

        public async Task EndMatch(int matchID, Teams team)
        {
            Lobby startedGame = null;
            foreach (var runningGame in StartedLobbies)
            {
                if (runningGame.Match.MatchID == matchID)
                {
                    startedGame = runningGame;
                    break;
                }
            }

            if (startedGame == null)
                throw new LeagueException($"Match #{matchID} not found");

            await CloseLobby(startedGame, team);
        }

        public async Task VoteWinner(Player player, Teams team)
        {
            if (!player.IsIngame)
                throw new LeagueException(player.User.Mention + " you are not in a game.");

            Lobby lobby = player.CurrentGame;
            if (lobby.BlueWinCalls.Contains(player))
            {
                if (team == Teams.Blue)
                {
                    throw new LeagueException(player.User.Mention + " you have already voted for this team.");
                }
                else
                {
                    lobby.BlueWinCalls.Remove(player);

                }
            }
            else if (lobby.RedWinCalls.Contains(player))
            {
                if (team == Teams.Red)
                {
                    throw new LeagueException(player.User.Mention + " you have already voted for this team.");
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
                    throw new LeagueException(player.User.Mention + " you have already voted for this team.");
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

            if (winner != Teams.None)
            {
                await CloseLobby(lobby, winner);
            }
        }

        public async Task ReCreateLobby(int matchID, Player playerToRemove)
        {
            Lobby startedGame = null;
            foreach (var runningGame in StartedLobbies)
            {
                if (runningGame.Match.MatchID == matchID)
                {
                    startedGame = runningGame;
                    break;
                }
            }

            if (startedGame == null)
                throw new LeagueException("Match not found");

            await CloseLobby(startedGame, Teams.Draw);

            Lobby lobby = null;
            if (!LobbyExists)
            {
                foreach (var player in startedGame.WaitingList)
                {
                    if (playerToRemove != player)
                    {
                        await CreateLobby(player, startedGame.League);
                        break;
                    }
                }
            }

            lobby = OpenLobby;

            if (lobby?.WaitingList.Count == 1)
            {
                foreach (var player in startedGame.WaitingList)
                {
                    if (player!= playerToRemove && !OpenLobby.WaitingList.Contains(player))
                        OpenLobby.AddPlayer(player);
                }
            }
            else
            {
                throw new LeagueException("There is already a open Lobby with more than 1 player, please rejoin yourself");
            }

            await LobbyChanged();
        }


        protected async Task OnLobbyHosted(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = LobbyHosted;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyClosed(Lobby lobby)
        {
            EventHandler<LobbyEventArgs> handler = LobbyClosed;
            LobbyEventArgs args = new LobbyEventArgs();
            args.Lobby = lobby;

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyFull(Lobby lobby)
        {
            EventHandler<LobbyEventArgs> handler = LobbyFull;
            LobbyEventArgs args = new LobbyEventArgs();
            args.Lobby = lobby;

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyCanceled(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = LobbyCanceled;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerJoined(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = PlayerJoined;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerLeft(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = PlayerLeft;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerKicked(Lobby lobby, Player player)
        {
            EventHandler<LobbyPlayerEventArgs> handler = PlayerKicked;
            LobbyPlayerEventArgs args = new LobbyPlayerEventArgs();
            args.Lobby = lobby;
            args.Player = player;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerVoted(Lobby lobby, Player player, Teams team)
        {
            EventHandler<LobbyVoteEventArgs> handler = PlayerVoted;
            LobbyVoteEventArgs args = new LobbyVoteEventArgs();
            args.Lobby = lobby;
            args.Player = player;
            args.Team = team;

            handler?.Invoke(this, args);
        }

        protected async Task OnPlayerVoted(Lobby lobby, Player player, Teams team, Teams previousVote)
        {
            EventHandler<LobbyVoteEventArgs> handler = PlayerVoted;
            LobbyVoteEventArgs args = new LobbyVoteEventArgs();
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

            handler?.Invoke(this, args);
        }

        protected async Task OnLobbyStarted(Lobby lobby)
        {
            EventHandler<LobbyEventArgs> handler = LobbyStarted;
            LobbyEventArgs args = new LobbyEventArgs();
            args.Lobby = lobby;

            handler?.Invoke(this, args);
        }

        protected async Task OnMatchEnded(Match matchResult)
        {
            EventHandler<MatchEventArgs> handler = MatchEnded;
            MatchEventArgs args = new MatchEventArgs();
            args.Match = matchResult;

            handler?.Invoke(this, args);
        }

    }

    public class LobbyEventArgs : EventArgs
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

    public class MatchEventArgs : EventArgs
    {
        public Match Match { get; set; }
    }
}
