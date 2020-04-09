using BanjoBotCore.Model;
using Discord;
using Discord.WebSocket;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static BanjoBotCore.Controller.DiscordMessageDispatcher;

namespace BanjoBotCore.Controller
{
    public class CommandController : ILeagueEventListener
    {
        //TODO: Add ChangeDiscord (Mod command) / ChangeSteam (Player command)
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(LeagueController));

        private const string STEAM_PROFILE_URL = "https://steamcommunity.com/profiles/";
        private readonly Dictionary<ulong, IUserMessage> _signups = new Dictionary<ulong, IUserMessage>();
        private readonly LeagueCoordinator _leagueCoordinator;
        private readonly DiscordMessageDispatcher _msgDispatcher;

        public CommandController(DiscordMessageDispatcher msgDispatcher, LeagueCoordinator leagueCoordinator)
        {
            _msgDispatcher = msgDispatcher;
            _leagueCoordinator = leagueCoordinator;
        }

        public async Task HostLobby(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);
            try
            {
                await lc.LobbyController.HostLobby(player, lc.League);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task JoinLobby(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);
            try
            {
                await lc.LobbyController.JoinLobby(player);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task LeaveLobby(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);
            try
            {
                await lc.LobbyController.LeaveLobby(player);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task KickPlayer(IMessageChannel channel, SocketGuildChannel socketGuildChannel, IUser playerToKick)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "This is no league channel.");
                return;
            }

            Player player = _leagueCoordinator.GetPlayerByDiscordID(playerToKick.Id);
            if (player == null)
            {
                await SendMessage(channel, "player not found");
                return;
            }

            try
            {
                await lc.LobbyController.KickPlayer(player);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task CancelLobby(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {

            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);
            try
            {
                if (player == player.CurrentGame?.Host || player.User.Roles.Contains(lc.League.LeagueDiscordConfig.ModeratorRole) || player.User.GuildPermissions.Administrator)
                    await lc.LobbyController.CancelLobbyByCommand(player);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task VoteCancel(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);
            try
            {
                await lc.LobbyController.VoteCancel(player);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task VoteMatchResult(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user, Teams team)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);

            try
            {
                await lc.LobbyController.VoteWinner(player, team);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task VoteDraw(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            await VoteMatchResult(channel, socketGuildChannel, user, Teams.Draw);
        }

        public async Task VoteWin(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);
            if (!player.IsIngame)
            {
                await SendMessage(channel, "You are not ingame");
                return;
            }

            Teams winner = player.CurrentGame.Match.GetTeam(Teams.Blue).Contains(player) ? Teams.Blue : Teams.Red;
            await VoteMatchResult(channel, socketGuildChannel, user, winner);
        }

        public async Task VoteLost(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);
            if (!player.IsIngame)
            {
                await SendMessage(channel, "You are not ingame");
                return;
            }
            Teams winner = player.CurrentGame.Match.GetTeam(Teams.Blue).Contains(player) ? Teams.Red : Teams.Blue;
            await VoteMatchResult(channel, socketGuildChannel, user, winner);
        }

        public async Task StartGame(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);
            try
            {
                await lc.LobbyController.StartGame(player);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task CloseLobbyByModerator(SocketGuildChannel socketGuildChannel, int matchID, Teams team)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(socketGuildChannel as IMessageChannel, "This is no league channel.");
                return;
            }

            try
            {
                await lc.LobbyController.EndMatch(matchID, team);
            }
            catch (LeagueException e)
            {
                await SendMessage(socketGuildChannel as IMessageChannel, e.Message);
            }
        }

        public async Task ListMatches(IMessageChannel channel, SocketGuildChannel socketGuildChannel)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);

            if (lc.LobbyController.LobbyExists)
                await SendTempMessage(channel, $"Open lobby: ({lc.LobbyController.OpenLobby.WaitingList.Count}/{Lobby.MAXPLAYERS})");
            else
                await SendTempMessage(channel, "No games in lobby.");

            if (lc.LobbyController.StartedLobbies.Count > 0)
            {
                String message = "Games in progress: ";
                foreach (var game in lc.LobbyController.StartedLobbies)
                {
                    message += $"#{game.MatchID}";
                }

                await SendTempMessage(channel, message);
            }
            else
            {
                await SendTempMessage(channel, "No games in progress.");
            }
        }

        public async Task ShowLobby(IMessageChannel channel, SocketGuildChannel socketGuildChannel)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);

            if (!lc.LobbyController.LobbyExists)
            {
                await SendTempMessage(channel, "No games open. Type !hostgame to create a game.");
                return;
            }

            String message = $"Lobby ({lc.LobbyController.OpenLobby.WaitingList.Count}/{Lobby.MAXPLAYERS})  players: \n";
            foreach (Player p in lc.LobbyController.OpenLobby.WaitingList)
            {
                message += p.PlayerMMRString(lc.League.LeagueID, lc.League.Season) + " ";
            }
            await SendTempMessage(channel, message);
        }

        public async Task ShowStats(IMessageChannel channel, SocketGuildChannel socketGuildChannel, IUser user, int season)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);

            if (season <= 0)
                season = lc.League.Season;

            PlayerStats stats = player.GetLeagueStats(lc.League.LeagueID, season);
            if (stats == null)
            {
                await SendTempMessage(channel, "No stats found");
                return;
            }

            int wins = stats.Wins;
            int losses = stats.Losses;
            int gamesPlayed = wins + losses;
            await SendTempMessage(channel, player.PlayerMMRString(lc.League.LeagueID, season) + " has " + gamesPlayed + " games played, " + wins + " wins, " + losses + " losses.\nCurrent win streak: " + stats.Streak + ".");
        }

        public async Task ShowPlayerProfile(SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            await ShowPlayerProfile(socketGuildChannel, user, lc.League.Season);
        }

        public async Task ShowPlayerProfile(SocketGuildChannel socketGuildChannel, SocketUser user, int season)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);

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

            List<Match> seasonMatches = player.GetMatchesBySeason(lc.League.LeagueID, season);
            if (seasonMatches.Count == 0)
            {
                await SendPrivateMessage(player.User as IGuildUser, "No stats found");
                return;
            }

            foreach (var match in seasonMatches)
            {
                MatchPlayerStats matchStats = await match.GetPlayerStats(player);
                if (matchStats.Match.StatsRecorded)
                {
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
            PlayerStats playerStats = player.GetLeagueStats(lc.League.LeagueID, season);

            int statCount = statsRecorded;

            // prevents null division
            if (statsRecorded == 0)
                statsRecorded = 1;

            string message = $"**{player.Name}'s Profile**\n`";
            message += $"{"League",-24} {lc.League.Name,-12} \n";
            message += $"{"Season",-24} {season,-12} \n";
            message += $"{"Matches",-24} {playerStats.MatchCount,-12} \n";
            message += $"{"Wins",-24} {playerStats.Wins,-12} \n";
            message += $"{"Losses",-24} {playerStats.Losses,-12} \n";
            message += $"{"Winrate",-24} {(float)playerStats.Wins / (float)playerStats.MatchCount,-12:P} \n";
            message += $"{"Streak",-24} {playerStats.Streak,-12} \n";
            message += $"{"Rating",-24} {playerStats.MMR,-12} `\n";
            message += "\n";
            message += $"**AverageStats**\n`";
            message += $"{"Goals",-24} {goals / statsRecorded,-12:N} \n";
            message += $"{"Assist",-24} {assist / statsRecorded,-12:N} \n";
            message += $"{"Steals",-24} {steals / statsRecorded,-12:N} \n";
            message += $"{"Turnovers",-24} {turnovers / statsRecorded,-12:N} \n";
            message += $"{"S-T",-24} {st / statsRecorded,-12:N} \n";
            message += $"{"Pickups",-24} {pickups / statsRecorded,-12:N} \n";
            message += $"{"Passes",-24} {passes / statsRecorded,-12:N} \n";
            message += $"{"PR",-24} {pr / statsRecorded,-12:N} \n";
            message += $"{"SaveRate",-24} {save / statsRecorded,-12:P} \n";
            message += $"{"Points",-24} {points / statsRecorded,-12:N} \n";
            message += $"{"PosT",-24} {post / statsRecorded,-12:N} \n";
            message += $"{"TAG",-24} {tag / statsRecorded,-12:N} \n`";
            message += $"\n*Stats for {statCount} of {playerStats.MatchCount} games were recorded*";

            await SendPrivateMessage(player.User as IGuildUser, message);
        }

        public async Task GetMatchHistory(SocketGuildChannel socketGuildChannel, SocketUser user, int season)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);

            object[] args = new object[] { "Date", "MatchID", "Goals", "Assist", "Steals", "Turnovers", "S/T", "Pickups", "Passes", "PR", "Save", "Points", "PosT", "TAG", "Mmr", "Streak", "Stats", "Hero" };
            String s = String.Format("{0,-12} {1,-8} {17,-10} {2,-8} {3,-8} {4,-8} {5,-10} {6,-8} {7,-8} {8,-8} {9,-8} {10,-8} {11,-8} {12,-8} {13,-8} {14,-8} {15,-8} {16,-8}\n", args);
            List<Match> allStats = player.GetMatchesBySeason(lc.League.LeagueID, season);
            IOrderedEnumerable<Match> orderedStats = allStats.OrderByDescending(match => match.Date);
            for (int i = 0; i < 10 && i < allStats.Count; i++)
            {
                Match match = orderedStats.ElementAt(i);
                MatchPlayerStats stats = await match.GetPlayerStats(player);
                args = new object[] { DateTime.Parse(stats.Match.Date.ToString()).ToShortDateString(), stats.Match.MatchID, stats.Goals, stats.Assist, stats.Steals, stats.Turnovers, stats.StealTurnDif, stats.Pickups, stats.Passes, stats.PassesReceived, stats.SaveRate, stats.Points, stats.PossessionTime, stats.TimeAsGoalie, stats.MmrAdjustment, stats.StreakBonus, stats.Match.StatsRecorded, stats.HeroID };
                s += String.Format("{0,-12} {1,-8} {17,-10} {2,-8} {3,-8} {4,-8} {5,-10} {6,-8} {7,-8} {8,-8} {9,-8} {10,-8:P0} {11,-8} {12,-8} {13,-8} {14,-8} {15,-8} {16,-8}\n", args);
            }
            if (!orderedStats.Any())
            {
                await SendPrivateMessage(player.User as IGuildUser, "You have not played in season " + season);
            }
            else
            {
                await SendPrivateMessage(player.User as IGuildUser, "```" + s + "```");
            }
        }

        public async Task GetMatchHistory(SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            await GetMatchHistory(socketGuildChannel, user, lc.League.Season);
        }

        //TODO: Only show players with at least 1 match
        public async Task ShowTopMMR(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user, int pSeason)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);

            int season = pSeason;
            if (season == -1)
                season = lc.League.Season;

            List<Player> leaderboard = await lc.GetLeaderBoard(season);

            bool inTopTen = false;
            string message = "Top 10 players by MMR: \n";
            int i = 0;
            foreach (Player p in leaderboard)
            {
                if (i < 10)
                {
                    if (p == player)
                        inTopTen = true;

                    message += "#" + (i + 1) + " " + p.PlayerMMRString(lc.League.LeagueID, season) + "\n";
                    i++;
                }
            }
            if (!inTopTen)
            {
                message += "-------------------------------------------------\n";
                int rank = leaderboard.IndexOf(player) + 1;
                message += "#" + rank + " " + player.PlayerMMRString(lc.League.LeagueID, season) + "\n";
            }
            await SendTempMessage(channel, message);
        }

        public async Task ShowRank(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(user.Id);

            List<Player> leaderboard = await lc.GetLeaderBoard();
            int rank = leaderboard.IndexOf(player) + 1;
            String message = "#" + rank + " " + player.PlayerMMRString(lc.League.LeagueID, lc.League.Season) + "\n";
            await SendTempMessage(channel, message);
        }

        public async Task ShowInteractiveLeaderboard()
        {
            throw new NotImplementedException();
        }

        public async Task ReCreateLobby(IMessageChannel channel, SocketGuildChannel socketGuildChannel, int matchID, IGuildUser playerToRemove)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            Player player = _leagueCoordinator.GetPlayerByDiscordID(playerToRemove.Id);
            if (lc == null)
            {
                await SendMessage(channel, "This is no league channel.");
                return;
            }

            try
            {
                await lc.LobbyController.ReCreateLobby(matchID, player);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
                return;
            }
        }

        public async Task RegisterPlayer(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketUser user, ulong steamID)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            log.Debug("Registration: " + user.Username + " SteamID(" + steamID + ")");
            if (lc == null)
            {
                await SendMessage(channel, "This is no league channel.");
                return;
            }

            try
            {
                await _leagueCoordinator.RegisterPlayer((SocketGuildUser)user, steamID);
                
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
                return;
            }
        }

        public async Task AcceptApplicant(IMessageChannel channel, SocketGuildChannel socketGuildChannel, IGuildUser guildUser)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "This is no league channel.");
                return;
            }

            Player player = _leagueCoordinator.GetApplicantByDiscordID(guildUser.Id);
            if (player == null)
            {
                await SendMessage(channel, "applicant not found");
                return;
            }

            try
            {
                await _leagueCoordinator.AcceptRegistration(player);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
                return;
            }

            await SendMessage(channel, player.User.Mention + "You got a private message!");
        }

        public async Task RejectApplicant(IMessageChannel channel, SocketGuildChannel socketGuildChannel, IGuildUser guildUser, String reasoning)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "This is no league channel.");
                return;
            }

            if (guildUser == null)
            {
                await SendMessage(channel, "usage: !decline @player [reason] [#channel] ");
                return;
            }

            Player player = _leagueCoordinator.GetApplicantByDiscordID(guildUser.Id);
            if (player == null)
            {
                await SendMessage(channel, "applicant not found");
                return;
            }

            try
            {
                await _leagueCoordinator.RejectRegistration(player, reasoning);
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
                return;
            }
        }

        public async Task StartNewSeason(IMessageChannel channel, SocketGuildChannel socketGuildChannel)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "This is no league channel.");
                return;
            }

            try
            {
                await lc.StartNewSeason();
            }
            catch (LeagueException e)
            {
                await SendMessage(channel, e.Message);
            }
        }

        public async Task ListLeagues(SocketGuildChannel channel)
        {
            object[] args = new object[] {"ID", "Name",
                    "Channel", "Role", "AutoAccept", "Steam",
                    "Season", "Matches", "Players",
                    "Applicants","ModRole"};
            String s = String.Format(
                        "{0,-4} {1,-10} {2,-10} {3,-10} {4,-12} {5,-8} {6,-8} {7,-8} {8,-8} {9,-10} \n", args);
            foreach (var lc in _leagueCoordinator.LeagueControllers)
            {
                string leaguechannel = lc.League.LeagueDiscordConfig.LeagueChannel != null ? lc.League.LeagueDiscordConfig.LeagueChannel.Name : "none";
                string role = _leagueCoordinator.DiscordServer.RegistrationRole != null ? _leagueCoordinator.DiscordServer.RegistrationRole.Name : "none";
                string modrole = lc.League.LeagueDiscordConfig.ModeratorRole != null ? lc.League.LeagueDiscordConfig.ModeratorRole.Name : "none";
                args = new object[] {lc.League.LeagueID, lc.League.Name,
                    leaguechannel, role, _leagueCoordinator.DiscordServer.AutoAccept, _leagueCoordinator.DiscordServer.NeedSteamToRegister,
                    lc.League.Season, _leagueCoordinator.Players.Count,
                    _leagueCoordinator.Applicants.Count, modrole};
                s += String.Format("{0,-4} {1,-10} {2,-10} {3,-12} {4,-10} {5,-8} {6,-8} {7,-8} {8,-8} {9,-8} {10,-10}\n", args);
            }
            await SendMessage(channel as IMessageChannel, "```" + s + "```");
        }

        public async Task ListApplicants(SocketGuildChannel channel)
        {
            object[] args = new object[] { "DiscordID", "Name", "SteamID", "Steam Profile", "League" };
            String s = String.Format(
                        "{0,-24} {1,-12} {2,-24} {3,-64} {4,-24}\n", args);
          
            foreach (var leagueApplicant in _leagueCoordinator.Applicants)
            {
                string name = leagueApplicant.User != null ? leagueApplicant.Name : "unknown";
                name = name.Length > 13 ? name.Substring(0, 12) : name;
                args = new object[] { leagueApplicant.discordID, name, leagueApplicant.SteamID, STEAM_PROFILE_URL + leagueApplicant.SteamID};
                String next = String.Format("{0,-24} {1,-12} {2,-24} {3,-64} {4,-24}\n", args);

                if (s.Length + next.Length > 2000)
                {
                    await SendMessage(channel as IMessageChannel, "```" + s + "```");
                    s = next;
                }
                else
                {
                    s += next;
                }
            }
            

            if (s.Length > 0)
                await SendMessage(channel as IMessageChannel, "```" + s + "```");
        }

        public async Task ListPlayers(IMessageChannel channel, SocketGuildChannel socketGuildChannel)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "League not found");
                return;
            }

            object[] args = new object[] { "DiscordID", "Name", "SteamID", "Matches", "M+D", "Wins", "Losses", "Rating" };
            String s = String.Format(
                "{0,-24} {1,-14} {2,-24} {3,-8} {4,-8} {5,-8} {6,-8} {7,-8}\n", args);

            foreach (var player in _leagueCoordinator.Players)
            {
                PlayerStats Stats = player.GetLeagueStats(lc.League.LeagueID, lc.League.Season);
                string name = player.User != null ? player.Name : "unknown";
                name = name.Length > 13 ? name.Substring(0, 12) : name;
                args = new object[]
                {
                    player.User.Id, name, player.SteamID,Stats.MatchCount,
                    player.Matches.Count,Stats.Wins, Stats.Losses, Stats.MMR
                };

                String next = String.Format("{0,-24} {1,-14} {2,-24} {3,-8} {4,-8} {5,-8} {6,-8}{7,-8}\n", args);

                if (s.Length + next.Length > 2000)
                {
                    await SendMessage(channel, "```" + s + "```");
                    s = next;
                }
                else
                {
                    s += next;
                }
            }

            if (s.Length > 0)
                await SendMessage(channel, "```" + s + "```");
        }

        public async Task SetModChannel(ITextChannel channel, SocketGuildChannel modChannel)
        {
            await _leagueCoordinator.SetModChannel(modChannel);
            await SendMessage(channel, "Moderator channel set to: " + modChannel.Name);
        }

        public async Task SetAutoAccept(IMessageChannel channel, SocketGuildChannel socketGuildChannel, Boolean autoAccept)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "Join a league channel first or pass a channel as a parameter #channel.");
                return;
            }

            await _leagueCoordinator.SetAutoAccept(autoAccept);
            await SendMessage(channel, "Autoaccept set to " + autoAccept);
        }

        public async Task SetSteamRegister(IMessageChannel channel, SocketGuildChannel socketGuildChannel, Boolean steamRegister)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "Join a league channel first or pass a channel as a parameter #channel.");
                return;
            }

            await _leagueCoordinator.SetSteamRegister(steamRegister);
        }

        public async Task CreateLeague(IMessageChannel channel, SocketGuildChannel leagueChannel, string name)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(leagueChannel);
            if (lc != null)
            {
                await SendMessage(channel, "This channel is already assigned to another league.");
                return;
            }

            LeagueDiscordConfig discordInfo = new LeagueDiscordConfig(leagueChannel);

            await _leagueCoordinator.CreateLeague(name, discordInfo);
            await SendMessage(channel, "League created.");
        }

        public async Task DeleteLeague(IMessageChannel channel, SocketGuildChannel socketGuildChannel)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "This channel is not assigned to league.");
                return;
            }

            await _leagueCoordinator.DeleteLeague(lc);
            await SendMessage(channel, "League deleted.");
        }

        public async Task SetLeagueChannel(IMessageChannel channel, SocketGuildChannel socketGuildChannel, SocketGuildChannel oldChannel)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(oldChannel);
            if (lc == null)
            {
                await SendMessage(channel, "This is no league channel.");
                return;
            }

            await lc.SetLeagueChannel(socketGuildChannel);
            await SendMessage(channel, "League updated.");
        }

        public async Task SetRegion(Region region)
        {
            //TODO: assign EU/ NA / ASIA / AUS
            //Regions will be the dota server locations
            //Could be used to find a optimal host
            throw new NotImplementedException();
        }

        public async Task SetRegistrationRole(IMessageChannel channel, SocketRole role = null)
        {
            await _leagueCoordinator.SetRegistrationRole(role);

            if (role == null)
            {
                await SendMessage(channel, "League role deleted.");
            }
            else
            {
                await SendMessage(channel, "League role assigned. New Role: " + role.Name);
            }

            foreach (var player in _leagueCoordinator.Players)
            {
                if (role == null && _leagueCoordinator.DiscordServer.RegistrationRole != null)
                {
                    await player.User.RemoveRoleAsync(_leagueCoordinator.DiscordServer.RegistrationRole);
                }
                else
                {
                    await AddDiscordRole(player, role);
                }
            }
        }

        public async Task SetModRole(IMessageChannel channel, SocketGuildChannel socketGuildChannel, IRole role = null)
        {
            LeagueController lc = _leagueCoordinator.GetLeagueController(socketGuildChannel);
            if (lc == null)
            {
                await SendMessage(channel, "This is no league channel.");
                return;
            }

            await lc.SetModRole((SocketRole)role);
            if (role == null)
            {
                await SendMessage(channel, "Mod role deleted.");
            }
            else
            {
                await SendMessage(channel, "Mod role assigned. New Role: " + role.Name);
            }
        }

        private async Task PrintGameResult(Match matchResult, IMessageChannel textChannel)
        {
            string message = "Closing lobby\n";

            switch (matchResult.Winner)
            {
                case Teams.Red:
                    message += "Red team has won BBL#" + matchResult.MatchID + "!\n";
                    message += await GetGameResultString(matchResult);
                    break;

                case Teams.Blue:
                    message += "Blue team has won BBL#" + matchResult.MatchID + "!\n";
                    message += await GetGameResultString(matchResult);

                    break;

                case Teams.Draw:
                    message += "Game BBL#" + matchResult.MatchID + " has ended in a draw. No stats have been recorded.";
                    break;
            }

            await SendMessage(textChannel, message);
        }

        private async Task<String> GetGameResultString(Match match)
        {
            Teams winner = match.Winner;
            League league = match.League;

            char blueSign = '+';
            char redSign = '+';
            if (Teams.Blue == winner)
                redSign = '-';
            else
                blueSign = '-';

            int mmrAdjustment = Math.Abs(match.PlayerMatchStats[0].MmrAdjustment);

            String message = "";
            message += "Blue team (" + blueSign + mmrAdjustment + "): ";
            foreach (var stats in match.PlayerMatchStats)
            {
                if (stats.Team == Teams.Blue)
                {
                    Player player = stats.Player;
                    if (player.GetLeagueStats(league.LeagueID, league.Season).Streak > 1)
                        message += player.PlayerMMRString(league.LeagueID, league.Season) + "+" + (2 * (player.GetLeagueStats(league.LeagueID, league.Season).Streak - 1)) + " ";
                    else
                        message += player.PlayerMMRString(league.LeagueID, league.Season) + " ";
                }
            }
            message += "\n";
            message += "Red team (" + redSign + mmrAdjustment + "): ";
            foreach (var stats in match.PlayerMatchStats)
            {
                if (stats.Team == Teams.Red)
                {
                    Player player = stats.Player;
                    if (player.GetLeagueStats(league.LeagueID, league.Season).Streak > 1)
                        message += player.PlayerMMRString(league.LeagueID, league.Season) + "+" + (2 * (player.GetLeagueStats(league.LeagueID, league.Season).Streak - 1)) + " ";
                    else
                        message += player.PlayerMMRString(league.LeagueID, league.Season) + " ";
                }
            }

            return message;
        }

        public async Task AddDiscordRole(Player player, IRole role)
        {
            if (role != null)
            {
                if (!player.User.Roles.Contains(role))
                {
                    await player.User.AddRoleAsync(role);
                }
            }
        }

        private async Task SendPrivateMessage(IGuildUser user, String message)
        {
            await (await user.GetOrCreateDMChannelAsync()).SendMessageAsync(message);
        }

        private async Task SendMessage(IMessageChannel textChannel, String message)
        {
            _msgDispatcher.AddQueue(new Message(textChannel, message));
            //return await textChannel.SendMessageAsync(message);
        }

        private async Task<IUserMessage> SendMessageImmediate(IMessageChannel textChannel, String message)
        {
            return await textChannel.SendMessageAsync(message);
        }

        private async Task SendTempMessage(IMessageChannel textChannel, String message)
        {
            IUserMessage discordMessage = await textChannel.SendMessageAsync(message);

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                DeleteMessage(discordMessage);
            }, null);
        }

        private void DeleteMessage(IUserMessage message)
        {
            System.Threading.Thread.Sleep(20 * 1000);
            message.DeleteAsync();
        }


        //TODO: Should these events be delegated to this calss?
        public async void LobbyClosed(object sender, LobbyEventArgs e)
        {
            if (e == null)
                return;

            if (e.Lobby.StartMessage != null)
            {
                await e.Lobby.StartMessage.UnpinAsync();
            }

            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            await SendMessage(channel, "Lobby closed");
        }

        public async void LobbyCreated(object sender, LobbyPlayerEventArgs e)
        {
            if (e == null)
                return;

            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            Player host = e.Lobby.Host;
            await SendMessage(channel, $"New Lobby created by {host.PlayerMMRString(e.Lobby.League.LeagueID, e.Lobby.League.Season)}. \nType !join to join the game. ({e.Lobby.WaitingList.Count}/{Lobby.MAXPLAYERS})");
        }

        public async void PlayerJoined(object sender, LobbyPlayerEventArgs e)
        {
            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            await SendMessage(channel, $"{e.Player.PlayerMMRString(e.Lobby.League.LeagueID, e.Lobby.League.Season)} has joined the lobby. ({e.Lobby.WaitingList.Count}/{Lobby.MAXPLAYERS})");
            await SendPrivateMessage(e.Player.User as IGuildUser, "Password for the Dota 2 lobby: " + e.Lobby.Password);
        }

        public async void PlayerLeft(object sender, LobbyPlayerEventArgs e)
        {
            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            await SendMessage(channel, $"{e.Player.PlayerMMRString(e.Lobby.League.LeagueID, e.Lobby.League.Season)} has left the lobby. ({e.Lobby.WaitingList.Count}/{Lobby.MAXPLAYERS})");
        }

        public async void PlayerKicked(object sender, LobbyPlayerEventArgs e)
        {
            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            await SendMessage(channel, $"{ e.Player.User.Mention} got kicked from the lobby. ({e.Lobby.WaitingList.Count}/{Lobby.MAXPLAYERS})");
        }

        public async void PlayerVotedCancel(object sender, LobbyPlayerEventArgs e)
        {
            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            await SendMessage(channel, "Vote recorded to cancel game by " + e.Player.Name + " (" + e.Lobby.CancelCalls.Count + "/" + e.Lobby.GetCancelThreshold() + ")");
        }

        public async void LobbyCanceled(object sender, LobbyPlayerEventArgs e)
        {
            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            await SendMessage(channel, "Lobby got canceled canceled");
        }

        public async void LobbyFull(object sender, LobbyEventArgs e)
        {
            foreach (var p in e.Lobby.WaitingList)
            {
                if (p == e.Lobby.Host)
                    await SendPrivateMessage(e.Lobby.Host.User as IGuildUser, $"The lobby is full. Please host a lobby in Dota 2 (Password: {e.Lobby.Password}). Once the lobby is full and ready type !startgame to get the teams");
                else
                    await SendPrivateMessage(p.User as IGuildUser, "The lobby is full, please join the Dota 2 lobby. Password: " + e.Lobby.Password);
            }
        }

        public async void LobbyStarted(object sender, LobbyEventArgs e)
        {
            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            String startmessage = "BBL#" + e.Lobby.Match.MatchID + " has been started.";
            String blueTeam = "Blue Team (" + e.Lobby.Match.GetTeamMMR(Teams.Blue) + "): ";
            foreach (var p in e.Lobby.Match.GetTeam(Teams.Blue))
            {
                blueTeam += p.User.Mention + "(" + p.GetLeagueStats(e.Lobby.League.LeagueID, e.Lobby.League.Season).MMR + ") ";
            }
            String redTeam = "Red Team (" + e.Lobby.Match.GetTeamMMR(Teams.Red) + "): ";
            foreach (var p in e.Lobby.Match.GetTeam(Teams.Red))
            {
                redTeam += p.User.Mention + "(" + p.GetLeagueStats(e.Lobby.League.LeagueID, e.Lobby.League.Season).MMR + ") ";
            }

            e.Lobby.StartMessage = await SendMessageImmediate(channel, startmessage + "\n" + blueTeam + "\n" + redTeam);
            await e.Lobby.StartMessage.PinAsync();
        }

        public async void PlayerVoted(object sender, LobbyVoteEventArgs e)
        {
            SocketTextChannel channel = e.Lobby.League.LeagueDiscordConfig.LeagueChannel as SocketTextChannel;
            String message = "";
            if (e.prevVote != Teams.None)
            {
                message += e.Player.Name + " has changed his Mind\n";
            }

            if (e.Team == Teams.Red)
            {
                message += $"Vote recorded for team Red in game #{e.Lobby.MatchID} by {e.Player.Name }. ({e.Lobby.RedWinCalls.Count}/{Lobby.VOTETHRESHOLD})";
            }
            else if (e.Team == Teams.Blue)
            {
                message += $"Vote recorded for team Blue in game #{e.Lobby.MatchID} by {e.Player.Name }. ({e.Lobby.BlueWinCalls.Count}/{Lobby.VOTETHRESHOLD})";
            }
            else
            {
                message += $"Draw vote recorded for game #{e.Lobby.MatchID} by {e.Player.Name }. ({e.Lobby.DrawCalls.Count}/{Lobby.VOTETHRESHOLD})";
            }

            await SendMessage(channel, message);
        }

        public async void MatchEnded(object sender, MatchEventArgs e)
        {
            if (e == null)
                return;

            IMessageChannel channel = e.Match.League.LeagueDiscordConfig.LeagueChannel as IMessageChannel;
            await PrintGameResult(e.Match, channel);
        }

        public async void PlayerRegistered(object sender, RegistrationEventArgs e)
        {
            if (e == null)
                return;

            await SendPrivateMessage(e.Player.User, "You sucessfully registered. Wait for the approval by a moderator");

            SocketGuildChannel modChannel = _leagueCoordinator.DiscordServer.ModeratorChannel;
            if (modChannel != null)
            {
                IUserMessage message = await SendMessageImmediate((IMessageChannel)modChannel, "New applicant: " + e.Player.User.Mention +
                    "\t" + STEAM_PROFILE_URL + e.Player.SteamID);
                _signups.Add(e.Player.User.Id, message);
                await message.PinAsync();
            }
        }

        public async void PlayerRegistrationFailed(object sender, RegistrationEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Message))
                await SendPrivateMessage(e.Player.User, "Your registration got rejected.\n Reason: " + e.Message);
            else
                await SendPrivateMessage(e.Player.User, "Your registration got rejected.\nTry again or contact a moderator");
        }

        public async void PlayerRegistrationAccepted(object sender, RegistrationEventArgs e)
        {
            await SendPrivateMessage(e.Player.User, "Your registration for got approved.\nYou can now start playing!" +
                "\n\n If you need help, ask a moderator or use !help \n\n Note: Please make sure you read the rules, you can find them in the channel #rules\n");

            _signups.TryGetValue(e.Player.User.Id, out IUserMessage message);
            await message?.UnpinAsync();
            _signups.Remove(e.Player.User.Id);

            await AddDiscordRole(e.Player, _leagueCoordinator.DiscordServer.RegistrationRole);
        }

        //TODO: Only display user with more than 1 match 
        public async void SeasonEnded(object sender, SeasonEventArgs e)
        {
            String message;
            List<Player> players = e.LeaderBoard;
            Player mostActive = null;
            int max = Int32.MinValue;
            foreach (var player in players)
            {
                if (player.GetLeagueStats(e.League.LeagueID, e.Season).MatchCount > max)
                {
                    mostActive = player;
                    max = player.GetLeagueStats(e.League.LeagueID, e.Season).MatchCount;
                }
            }

            string mentionMostActive = mostActive == null ? " :( " : mostActive.Name;
            message = "**Season " + e.Season + " has ended.**\n";
            message += "Big thanks to our most active player " + mentionMostActive + " with " + max + " matches \n\n";
            message += "**Top Players Season " + e.Season + ": **\n";
            await SendMessage(e.League.LeagueDiscordConfig.LeagueChannel as IMessageChannel, message);

            object[] args = new object[] { "Rank", "Name", "MMR", "Matches", "Wins", "Losses" };
            String leaderboard = String.Format("{0,-7} {1,-30} {2,-10} {3,-10} {4,-10} {5,-10}\n", args);
            int rank = 0;
            foreach (Player player in players)
            {
                rank++;
                PlayerStats stats = player.GetLeagueStats(e.League.LeagueID, e.Season);
                string name = player.User != null ? player.Name : "unknown";
                name = name.Length > 19 ? name.Substring(0, 19) : name;
                args = new object[] { rank, name, stats.MMR, stats.MatchCount, stats.Wins, stats.Losses };
                String nextLine = String.Format("#{0,-6} {1,-30} {2,-10} {3,-10} {4,-10} {5,-10}\n", args);

                if (leaderboard.Length + nextLine.Length > 2000)
                {
                    await SendMessage(e.League.LeagueDiscordConfig.LeagueChannel as IMessageChannel, "```" + leaderboard + "```");
                    leaderboard = "";
                }

                leaderboard += nextLine;
            }

            if (leaderboard.Length > 0)
            {
                await SendMessage(e.League.LeagueDiscordConfig.LeagueChannel as IMessageChannel, "```" + leaderboard + "```");
            }
        }
    }
}