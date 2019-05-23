using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.DependencyInjection;
using BanjoBotCore.Model;

namespace BanjoBotCore
{
    //TODO: Exception handling -> try catch in concrete methods like UpdateMatch, InsertLobby for better Error logging
    //TODO: Query methods should not have any mode data -> DAO
    public class DatabaseController 
    {
        private static readonly ILog log = log4net.LogManager.GetLogger(typeof(DatabaseController));
        private string _connectionString = "server=127.0.0.1;database=banjoball;user=banjo_admin;pwd=D2bblXX;";


        //public DatabaseController(IServiceProvider serviceProvider) 
        //{
        //    IConfiguration config = serviceProvider.GetService<IConfiguration>();
            
        //    _connectionString = config.GetValue<String>("DbConnectionString");

        //}
        public DatabaseController()
        {

        }

        public async Task<int> ExecuteNoQuery(MySqlCommand command)
        {
            MySqlConnection connection = new MySqlConnection(_connectionString);
            command.Connection = connection;
            try
            {
                connection.Open();
                using (command)
                {
                    return await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception e)
            {
                connection.Close();
                log.Error("ExecuteNoQuery: " + command.CommandText);
                log.Error(e.ToString());
                throw new Exception("Error: Couldn't save data");

            }
        }

        public async Task<int> ExecuteScalar(MySqlCommand command)
        {
            MySqlConnection connection = new MySqlConnection(_connectionString);
            command.Connection = connection;
            try
            {
                connection.Open();
                using (command)
                {
                    return Convert.ToInt32(await command.ExecuteScalarAsync());
                }
            }
            catch (Exception e)
            {
                connection.Close();
                log.Error("ExecuteScalar: " + command.CommandText);
                log.Error(e.ToString());
                throw new Exception("Error: Couldn't save data");
            }
        }

        public async Task<MySqlDataReader> ExecuteReader(MySqlCommand command)
        {
            
            MySqlConnection connection = new MySqlConnection(_connectionString);
            command.Connection = connection;
            try
            {
                connection.Open();
                using (command)
                {
                    return (MySqlDataReader)await command.ExecuteReaderAsync();
                }
            }
            catch (Exception e)
            {
                connection.Close();
                log.Error("ExecuteReader: " + command.CommandText);
                log.Error(e.ToString());
                throw new Exception("Error: Couldn't save data");
            }
        }

        public async Task UpdateMatch(Match match)
        {
            log.Debug("Update Match");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "update matches set duration=@duration, date=@date, winner=@winner, steam_match_id=@steam_match_id, stats_recorded=@stats_recorded where match_id=@match_id";
            command.Parameters.AddWithValue("@duration", match.Duration);
            command.Parameters.AddWithValue("@date", match.Date);
            command.Parameters.AddWithValue("@winner", match.Winner);
            command.Parameters.AddWithValue("@steam_match_id", match.SteamMatchID);
            command.Parameters.AddWithValue("@match_id", match.MatchID);
            command.Parameters.AddWithValue("@stats_recorded", match.StatsRecorded);
            await ExecuteNoQuery(command);


            //TODO: Replace -> UpdateMatchPlayer / InserMatchPlayer() 
            StringBuilder queryBuilder =
                new StringBuilder(
                    "REPLACE INTO match_player_stats (steam_id, match_id,hero_id, goals, assist, steals, turnovers, steal_turnover_difference,pickups,passes, passes_received, save_rate, points, possession_time, time_as_goalie, win, mmr_adjustment, streak_bonus,team) VALUES ");
            for (int i = 0; i < match.PlayerMatchStats.Count; i++)
            {
                queryBuilder.AppendFormat(
                    "(@steam_id{0},@match_id{0},@hero_id{0},@goals{0},@assist{0},@steals{0},@turnovers{0},@steal_turnover_difference{0},@pickups{0},@passes{0},@passes_received{0},@save_rate{0},@points{0},@possession_time{0},@time_as_goalie{0},@win{0},@mmr_adjustment{0},@streak_bonus{0},@team{0}),",
                    i);
                if (i == match.PlayerMatchStats.Count - 1)
                {
                    queryBuilder.Replace(',', ';', queryBuilder.Length - 1, 1);
                }
            }

            command = new MySqlCommand(queryBuilder.ToString());

            for (int i = 0; i < match.PlayerMatchStats.Count; i++)
            {
                command.Parameters.AddWithValue("@steam_id" + i, match.PlayerMatchStats[i].SteamID);
                command.Parameters.AddWithValue("@match_id" + i, match.MatchID);
                command.Parameters.AddWithValue("@hero_id" + i, match.PlayerMatchStats[i].HeroID);
                command.Parameters.AddWithValue("@goals" + i, match.PlayerMatchStats[i].Goals);
                command.Parameters.AddWithValue("@assist" + i, match.PlayerMatchStats[i].Assist);
                command.Parameters.AddWithValue("@steals" + i, match.PlayerMatchStats[i].Steals);
                command.Parameters.AddWithValue("@turnovers" + i, match.PlayerMatchStats[i].Turnovers);
                command.Parameters.AddWithValue("@steal_turnover_difference" + i, match.PlayerMatchStats[i].StealTurnDif);
                command.Parameters.AddWithValue("@pickups" + i, match.PlayerMatchStats[i].Pickups);
                command.Parameters.AddWithValue("@passes" + i, match.PlayerMatchStats[i].Passes);
                command.Parameters.AddWithValue("@passes_received" + i, match.PlayerMatchStats[i].PassesReceived);
                command.Parameters.AddWithValue("@save_rate" + i, match.PlayerMatchStats[i].SaveRate);
                command.Parameters.AddWithValue("@points" + i, match.PlayerMatchStats[i].Points);
                command.Parameters.AddWithValue("@possession_time" + i, match.PlayerMatchStats[i].PossessionTime);
                command.Parameters.AddWithValue("@time_as_goalie" + i, match.PlayerMatchStats[i].TimeAsGoalie);
                command.Parameters.AddWithValue("@win" + i, match.PlayerMatchStats[i].Win);
                command.Parameters.AddWithValue("@mmr_adjustment" + i, match.PlayerMatchStats[i].MmrAdjustment);
                command.Parameters.AddWithValue("@streak_bonus" + i, match.PlayerMatchStats[i].StreakBonus);
                command.Parameters.AddWithValue("@team" + i, match.PlayerMatchStats[i].Team);

            }

            await ExecuteNoQuery(command);
        }

        public async Task UpdateLobby(Lobby lobby)
        {
            log.Debug("Update lobby");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "update lobbies set match_id=@match_id, has_started=@has_started, closed=@closed where lobby_id=@lobby_id";
            if(lobby.Match == null)
                command.Parameters.AddWithValue("@match_id",  DBNull.Value);
            else
                command.Parameters.AddWithValue("@match_id", lobby.Match.MatchID);
            command.Parameters.AddWithValue("@has_started", lobby.HasStarted);
            command.Parameters.AddWithValue("@closed", lobby.IsClosed);
            await ExecuteNoQuery(command);

            command = new MySqlCommand();
            var params1 = new string[lobby.WaitingList.Count];
            for (int i = 0; i < lobby.WaitingList.Count; i++)
            {
                params1[i] = string.Format("@steam_id{0}", i);
                command.Parameters.AddWithValue(params1[i], lobby.WaitingList[i].SteamID);
            }
            command.CommandText = String.Format("Delete from lobby_players where steam_id NOT IN({0})", string.Join(",",params1));
            await ExecuteNoQuery(command);

            //TODO: Replace -> UpdateLobbyPlayers() / InsertLobbyPlayer() 
            StringBuilder queryBuilder =
                new StringBuilder(
                    "Replace into lobby_players (lobby_id, steam_id, cancel_call, red_win_call, blue_win_call, draw_call) VALUES ");
            for (int i = 0; i < lobby.WaitingList.Count; i++)
            {
                queryBuilder.AppendFormat(
                    "(@lobby_id{0}, @steam_id{0} @cancel_call{0}, @red_win_call{0}, @blue_win_call{0}, @draw_call{0}),",
                    i);
                if (i == lobby.WaitingList.Count - 1)
                {
                    queryBuilder.Replace(',', ';', queryBuilder.Length - 1, 1);
                }
            }

            command = new MySqlCommand(queryBuilder.ToString());

            for (int i = 0; i < lobby.WaitingList.Count; i++)
            {
                command.Parameters.AddWithValue("@lobby_id" + i, lobby.LobbyID);
                command.Parameters.AddWithValue("@steam_id" + i, lobby.WaitingList[i].SteamID);
                command.Parameters.AddWithValue("@cancel_call" + i, lobby.CancelCalls.Contains(lobby.WaitingList[i]));
                command.Parameters.AddWithValue("@red_win_call" + i, lobby.RedWinCalls.Contains(lobby.WaitingList[i]));
                command.Parameters.AddWithValue("@blue_win_call" + i, lobby.BlueWinCalls.Contains(lobby.WaitingList[i]));
                command.Parameters.AddWithValue("@draw_call" + i, lobby.DrawCalls.Contains(lobby.WaitingList[i]));
            }
            await ExecuteNoQuery(command);
        }

        public async Task UpdateLeague(League league)
        {
            log.Debug("UpdateLeague");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Update leagues Set season=@season,name=@name " +
                "Where league_id = @league_id";
            command.Parameters.AddWithValue("@league_id", league.LeagueID);
            command.Parameters.AddWithValue("@season", league.Season);
            command.Parameters.AddWithValue("@name", league.Name);
            await ExecuteNoQuery(command);

            if(league.DiscordInformation == null)
                return;

            command = new MySqlCommand();
            command.CommandText =
                "Replace into discord_information (server_id,league_id,channel_id,role_id,auto_accept,need_steam_register,mod_role_id,moderator_channel) values (@server_id, @league_id,@channel_id,@role_id,@autoaccept,@steamreg,@modrole,@moderator_channel)";
            command.Parameters.AddWithValue("@league_id", league.LeagueID);
            command.Parameters.AddWithValue("@server_id", league.DiscordInformation.DiscordServer.Id);
            command.Parameters.AddWithValue("@autoaccept", league.DiscordInformation.AutoAccept);
            command.Parameters.AddWithValue("@steamreg", league.DiscordInformation.NeedSteamToRegister);
            if (league.DiscordInformation.ModeratorRole == null)
                command.Parameters.AddWithValue("@modrole", DBNull.Value);
            else
                command.Parameters.AddWithValue("@modrole", league.DiscordInformation.ModeratorRole.Id);
            if (league.DiscordInformation.LeagueRole == null)
                command.Parameters.AddWithValue("@role_id", DBNull.Value);
            else
                command.Parameters.AddWithValue("@role_id", league.DiscordInformation.LeagueRole.Id);
            if (league.DiscordInformation.Channel == null)
                command.Parameters.AddWithValue("@channel_id", DBNull.Value);
            else
                command.Parameters.AddWithValue("@channel_id", league.DiscordInformation.Channel.Id);
            if (league.DiscordInformation.ModeratorChannel == null)
                command.Parameters.AddWithValue("@moderator_channel", DBNull.Value);
            else
                command.Parameters.AddWithValue("@moderator_channel", league.DiscordInformation.ModeratorChannel.Id);

            await ExecuteNoQuery(command);

        }

        public async Task UpdatePlayerStats(Player player, PlayerStats playerstats)
        {
            log.Debug("UpdatePlayerStats");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "REPLACE INTO player_stats (league_id,season,steam_id,matches,wins,losses,mmr,streak) Values (@league_id,@season,@steam_id,@matches,@wins,@losses, @mmr,@streak)";
            command.Parameters.AddWithValue("@matches", playerstats.MatchCount);
            command.Parameters.AddWithValue("@wins", playerstats.Wins);
            command.Parameters.AddWithValue("@losses", playerstats.Losses);
            command.Parameters.AddWithValue("@mmr", playerstats.MMR);
            command.Parameters.AddWithValue("@streak", playerstats.Streak);
            command.Parameters.AddWithValue("@league_id", playerstats.LeagueID);
            command.Parameters.AddWithValue("@steam_id", player.SteamID);
            command.Parameters.AddWithValue("@season", playerstats.Season);

            await ExecuteNoQuery(command);

        }
        
        public async Task UpdatePlayer(Player player)
        {
            log.Debug("UpdatePlayer");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Update players where discord_id=@discordID set steam_id=@steamID";
            command.Parameters.AddWithValue("@steamID", player.SteamID);
            command.Parameters.AddWithValue("@discordID", player.User.Id);
            Console.WriteLine("Change steamID (" + player.User.Username + ")");
            await ExecuteNoQuery(command);
        }

        public async Task InsertRegistrationToLeague(Player player, League league)
        {
            log.Debug("Try RegisterPlayerToLeague SteamID(" + player.SteamID + "DiscordID) " + player.User.Id + "UserName(" + player.User.Username + ")");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Insert into players_leagues (steam_id, league_id, approved) Values (@steam_id,@league_id,1) ON DUPLICATE KEY UPDATE approved=1";
            command.Parameters.AddWithValue("@steam_id", player.SteamID);
            command.Parameters.AddWithValue("@league_id", league.LeagueID);
            await ExecuteNoQuery(command);
        }

        public async Task DeleteRegistration(ulong steamID, League league)
        {
            log.Debug("DeclineRegistration");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Delete from players_leagues where steam_id=@steam_id AND league_id=@league_id";
            command.Parameters.AddWithValue("@steam_id", steamID);
            command.Parameters.AddWithValue("@league_id", league.LeagueID);
            await ExecuteNoQuery(command);
        }

        public async Task InsertSignupToLeague(ulong steamID, League league)
        {
            log.Debug("InsertSignupToLeague");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Insert into players_leagues (steam_id, league_id, approved) Values (@steam_id,@league_id,0)";
            command.Parameters.AddWithValue("@steam_id", steamID);
            command.Parameters.AddWithValue("@league_id", league.LeagueID);
            await ExecuteNoQuery(command);
        }

        public async Task<int> InsertLobby(Lobby lobby)
        {
            log.Debug("Insert new lobby");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Insert into lobbies (league_id, datetime, steam_id, password) Values (@league_id ,@datetime,@steam_id,@password); SELECT LAST_INSERT_ID()";
            command.Parameters.AddWithValue("@league_id", lobby.League.LeagueID);
            command.Parameters.AddWithValue("@datetime", DateTime.Now);
            command.Parameters.AddWithValue("@steam_id", lobby.Host.SteamID);
            command.Parameters.AddWithValue("@password", lobby.Password);
            int lobbyID = await ExecuteScalar(command);

            StringBuilder queryBuilder = new StringBuilder("Insert into match_player_stats (lobby_id, steam_id, cancel_call, red_win_call, blue_win_call, draw_call) VALUES ");
            List<Player> players = lobby.WaitingList;
            for (int i = 0; i < players.Count; i++)
            {
                queryBuilder.AppendFormat("(@lobby_id{0},@steam_id{0},@cancel_call{0},@red_win_call{0},@blue_win_call{0},@draw_call{0}),", i);
                if (i == players.Count - 1)
                {
                    queryBuilder.Replace(',', ';', queryBuilder.Length - 1, 1);
                }
            }

            command = new MySqlCommand(queryBuilder.ToString());

            for (int i = 0; i < players.Count; i++)
            {
                command.Parameters.AddWithValue("@lobby_id" + i, lobby.League.LeagueID);
                command.Parameters.AddWithValue("@steam_id" + i, players[i].SteamID);
                command.Parameters.AddWithValue("@cancel_call" + i, lobby.CancelCalls.Contains(players[i]));
                command.Parameters.AddWithValue("@red_win_call" + i, lobby.RedWinCalls.Contains(players[i]));
                command.Parameters.AddWithValue("@blue_win_call" + i, lobby.BlueWinCalls.Contains(players[i]));
                command.Parameters.AddWithValue("@draw_call" + i, lobby.DrawCalls.Contains(players[i]));
            }

            await ExecuteNoQuery(command);

            return lobbyID;
        }

        public async Task<int> InsertMatch(Match match)
        {
            log.Debug("Insert new match");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Insert into matches (season, league_id, date) Values (@season,@league_id,@date); SELECT LAST_INSERT_ID()";
            command.Parameters.AddWithValue("@season", match.Season);
            command.Parameters.AddWithValue("@league_id", match.LeagueID);
            command.Parameters.AddWithValue("@date", DateTime.Now);
            int matchID = await ExecuteScalar(command);

            StringBuilder queryBuilder = new StringBuilder("Insert into match_player_stats (steam_id,match_id, team) VALUES ");
            List<MatchPlayerStats> stats = match.PlayerMatchStats;
            for (int i = 0; i < stats.Count; i++)
            {
                queryBuilder.AppendFormat("(@steam_id{0},@match_id{0},@team{0}),", i);
                if (i == stats.Count - 1)
                {
                    queryBuilder.Replace(',', ';', queryBuilder.Length - 1, 1);
                }
            }

            command = new MySqlCommand(queryBuilder.ToString());

            for (int i = 0; i < stats.Count; i++)
            {
                command.Parameters.AddWithValue("@steam_id" + i, stats[i].Player.SteamID);
                command.Parameters.AddWithValue("@match_id" + i, matchID);
                command.Parameters.AddWithValue("@team" + i, stats[i].Team);
            }

            await ExecuteNoQuery(command);

            return matchID;
        }

        public async Task InsertPlayer(Player player)
        {
            log.Debug("Try InsertNewPlayer SteamID(" + player.SteamID + ") DiscordID(" + player.User.Id + ") UserName(" + player.User.Username + ")");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Insert into players (discord_id, steam_id) Values (@discordID,@steamID) ON DUPLICATE KEY UPDATE steam_id=@steamID";
            command.Parameters.AddWithValue("@steamID", player.SteamID);
            command.Parameters.AddWithValue("@discordID", player.User.Id);
            await ExecuteNoQuery(command);
            log.Debug("New player inserted = SteamID(" + player.SteamID + ") DiscordID(" + player.User.Id + ")UserName(" + player.User.Username + ")");
        }

        public async Task<int> InsertLeague()
        {
            log.Debug("InsertNewLeague");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Insert into leagues (season) Values (@season);SELECT LAST_INSERT_ID();";
            command.Parameters.AddWithValue("@season", 1);

            return await ExecuteScalar(command);
        }

        public async Task<List<League>> GetLeagues()
        {
            log.Debug("GetLeagues");
            MySqlCommand command = new MySqlCommand();
            command.CommandText = "select *," +
                                  "(select count(*) from matches m where l.league_id = m.league_id AND l.season = m.season) as match_count" +
                                  " from leagues l left JOIN discord_information di on l.league_id = di.league_id";

            List<League> leagues = new List<League>();
            using (MySqlDataReader reader = await ExecuteReader(command))
            {
                while (reader.Read())
                {
                    int leagueID = 0;
                    ulong server_id = ulong.MinValue;
                    ulong channel = ulong.MinValue;
                    ulong modChannel = ulong.MinValue;
                    ulong role = ulong.MinValue;
                    ulong modRoleID = ulong.MinValue;
                    int season = 0;
                    string name = "";
                    bool autoAccept = false;
                    bool needSteamReg = false;
                    int match_count = 0;

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (!reader.IsDBNull(i) && reader.GetName(i).Equals("league_id"))
                        {
                            leagueID = reader.GetInt32(i);
                        }
                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("server_id")) {
                            server_id = reader.GetUInt64(i);
                        }
                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("channel_id"))
                        {
                            channel = reader.GetUInt64(i);
                        }
                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("moderator_channel")) {
                            modChannel = reader.GetUInt64(i);
                        }

                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("mod_role_id")) {
                            modRoleID = reader.GetUInt64(i);
                        }
                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("role_id"))
                        {
                            role = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("season"))
                        {
                            season = reader.GetInt32(i);
                        }
                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("name"))
                        {
                            name = reader.GetString(i);
                        }
                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("auto_accept"))
                        {
                            autoAccept = reader.GetBoolean(i);
                        }
                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("need_steam_register"))
                        {
                            needSteamReg = reader.GetBoolean(i);
                        }
                        else if (!reader.IsDBNull(i) && reader.GetName(i).Equals("match_count"))
                        {
                            match_count = reader.GetInt32(i);
                        }

                    }
                    League league = new League(leagueID, name, season, match_count);
                    if (server_id != ulong.MinValue)
                    {
                        league.DiscordInformation = new DiscordInformation(server_id, channel, modRoleID, role, modChannel,autoAccept,needSteamReg);
                    }
                    leagues.Add(league);
                }
            }

            return leagues;

        }

        public async Task<List<Player>> GetPlayerBase(int leagueID)
        {
            log.Debug("GetPlayerBase");
            List<Player> result = new List<Player>();
            MySqlCommand command = new MySqlCommand();

            command.CommandText = string.Format("Select * from players p " +
                                                "inner join players_leagues pl on p.steam_id = pl.steam_id " +
                                                "inner join player_stats ps on p.steam_id = ps.steam_id and pl.league_id=ps.league_id " +
                                                "where pl.league_id = @leagueID " +
                                                "AND pl.approved = 1");

            command.Parameters.AddWithValue("@leagueID", leagueID);

            using (MySqlDataReader reader = await ExecuteReader(command))
            {      
                while (reader.Read()) {
                    ulong discordId = ulong.MinValue;
                    ulong steamId = 0;
                    int season = 0;
                    int streak = 0;
                    int matches = 0;
                    int wins = 0;
                    int losses = 0;
                    int mmr = 0;
                    int leagueId = 0;

                    for (int i = 0; i < reader.FieldCount; i++) {
                        if (!reader.IsDBNull(i) && reader.GetName(i).Equals("discord_id")) {
                            discordId = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("steam_id")) {
                            steamId = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("season")) {
                            season = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("streak")) {
                            streak = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("matches")) {
                            matches = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("wins")) {
                            wins = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("losses")) {
                            losses = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("mmr")) {
                            mmr = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("league_id")) {
                            leagueId = reader.GetInt32(i);
                        }
                    }

                    Player existingPlayer = null;
                    foreach (var p in result) {
                        if (p.SteamID == steamId) {
                            existingPlayer = p;
                            existingPlayer.PlayerStats.Add(new PlayerStats(leagueId, season, matches, wins, losses, mmr, streak));
                            break;
                        }
                    }
                    if (existingPlayer == null) {
                        Player player = new Player(discordId,steamId);
                        player.PlayerStats.Add(new PlayerStats(leagueId, season, matches, wins, losses, mmr, streak));
                        result.Add(player);
                    }
                    

                }
            }

            return result;
        }

        public async Task<List<Player>> GetApplicants(int leagueID)
        {
            log.Debug("GetApplicants");
            List<Player> result = new List<Player>();
            MySqlCommand command = new MySqlCommand();
            command.CommandText = "Select * from players p " +
                                  "inner join players_leagues pl on p.steam_id = pl.steam_id " +
                                  "where pl.league_id = @league_id " +
                                  "AND pl.approved = 0";

            command.Parameters.AddWithValue("@league_id", leagueID);

            using (MySqlDataReader reader = await ExecuteReader(command))
            {
                while (reader.Read()) {
                    ulong discordId = 0;
                    ulong steamId = 0;

                    for (int i = 0; i < reader.FieldCount; i++) {
                        if (reader.GetName(i).Equals("discord_id")) {
                            discordId = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("steam_id")) {
                            steamId = reader.GetUInt64(i);
                        }
                    }

                    result.Add(new Player(discordId, steamId));
                    
                }
            }
            return result;
        }


        public async Task<List<Lobby>> GetLobbies(int leagueID, List<Player> players)
        {
            log.Debug("GetLobbies");
            List<Lobby> lobbies = new List<Lobby>();
            MySqlCommand command = new MySqlCommand();
            command.CommandText = string.Format("Select * from lobbies l " +
                                                "inner join lobby_players lp on l.lobby_id = lp.lobby_id " +
                                                "where l.league_id = @league_id " +
                                                "AND closed = false");
            command.Parameters.AddWithValue("@league_id", leagueID);

            using (MySqlDataReader reader = await ExecuteReader(command))
            {
                while (reader.Read())
                {
                    int lobby_id = 0;
                    int league_id = 0;
                    int match_id = 0;
                    String password = "";
                    Boolean has_started = false;
                    DateTime date = DateTime.MaxValue;
                    ulong steam_id_host = ulong.MinValue;
                    ulong discord_message = ulong.MinValue;
                    ulong steam_id = ulong.MinValue;
                    Boolean cancel_call = false;
                    Boolean red_win_call = false;
                    Boolean blue_win_call = false;
                    Boolean draw_call = false;

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.GetName(i).Equals("lobby_id"))
                        {
                            lobby_id = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("league_id"))
                        {
                            league_id = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("date"))
                        {
                            date = reader.GetDateTime(i);
                        }
                        else if (reader.GetName(i).Equals("steam_id_host"))
                        {
                            steam_id_host = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("match_id"))
                        {
                            match_id = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("password"))
                        {
                            password = reader.GetString(i);
                        }
                        else if (reader.GetName(i).Equals("discord_message"))
                        {
                            discord_message = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("has_started"))
                        {
                            has_started = reader.GetBoolean(i);
                        }
                        else if (reader.GetName(i).Equals("steam_id"))
                        {
                            steam_id = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("cancel_call"))
                        {
                            cancel_call = reader.GetBoolean(i);
                        }
                        else if (reader.GetName(i).Equals("red_win_call"))
                        {
                            red_win_call = reader.GetBoolean(i);
                        }
                        else if (reader.GetName(i).Equals("blue_win_call"))
                        {
                            blue_win_call = reader.GetBoolean(i);
                        }
                        else if (reader.GetName(i).Equals("draw_call"))
                        {
                            draw_call = reader.GetBoolean(i);
                        }
                    }
                    Lobby lobby = null;
                    foreach (var l in lobbies)
                    {
                        if (l.LobbyID == lobby_id)
                        {
                            lobby = l;
                        }
                    }
                    if (lobby == null)
                    {
                        lobby = new Lobby(lobby_id, league_id, steam_id_host,match_id, has_started,password, discord_message);
                        lobbies.Add(lobby);
                    }
                    Player player = players.Find(p => p.SteamID == steam_id);
                    if(player != null)
                    {
                        lobby.WaitingList.Add(player);
                        if(blue_win_call)
                            lobby.BlueWinCalls.Add(player);
                        if(red_win_call)
                            lobby.RedWinCalls.Add(player);
                        if (draw_call)
                            lobby.DrawCalls.Add(player);
                        if (cancel_call)
                            lobby.CancelCalls.Add(player);
                    }
                        

                }
            }
            return lobbies;
        }

        public async Task<List<Match>> GetMatchHistory(int leagueID)
        {
            log.Debug("GetMatchHistory");
            List<Match> matches = new List<Match>();
            MySqlCommand command = new MySqlCommand();
            command.CommandText = string.Format("Select * from matches m " +
                                                "inner join match_player_stats ps on m.match_id = ps.match_id " +
                                                "where m.league_id = @league_id");
            command.Parameters.AddWithValue("@league_id", leagueID);

            using (MySqlDataReader reader = await ExecuteReader(command))
            {
                while (reader.Read()) {
                    int match_id = 0;
                    int duration = 0;
                    DateTime date = DateTime.MaxValue;
                    ulong steam_match_id = ulong.MinValue;
                    int season = 0;
                    ulong steam_id = 0;
                    int heroID = -1;
                    int goals = 0;
                    int assist = 0;
                    int steals = 0;
                    int turnovers = 0;
                    int steal_turnover_difference = 0;
                    int pickups = 0;
                    int passes = 0;
                    int passes_received = 0;
                    float save_rate = 0;
                    int points = 0;
                    int possession_time = 0;
                    int time_as_goalie = 0;
                    bool win = false;
                    int mmr_adjustment = 0;
                    int streak_bonus = 0;
                    Teams winner = Teams.None;
                    bool statsRecorded = false;
                    Teams team = 0;

                    for (int i = 0; i < reader.FieldCount; i++) {
                        if (reader.GetName(i).Equals("match_id")) {
                            match_id = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("duration")) {
                            duration = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("date")) {
                            date = reader.GetDateTime(i);
                        }
                        else if (reader.GetName(i).Equals("steam_match_id")) {
                            steam_match_id = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("season")) {
                            season = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("steam_id")) {
                            steam_id = reader.GetUInt64(i);
                        }
                        else if (reader.GetName(i).Equals("hero_id")) {
                            heroID = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("goals")) {
                            goals = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("assist")) {
                            assist = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("steals")) {
                            steals = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("turnovers")) {
                            turnovers = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("steal_turnover_difference")) {
                            steal_turnover_difference = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("pickups")) {
                            pickups = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("passes")) {
                            passes = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("passes_received")) {
                            passes_received = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("save_rate")) {
                            save_rate = reader.GetFloat(i);
                        }
                        else if (reader.GetName(i).Equals("points")) {
                            points = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("possession_time")) {
                            possession_time = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("time_as_goalie")) {
                            time_as_goalie = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("win")) {
                            win = reader.GetBoolean(i);
                        }
                        else if (reader.GetName(i).Equals("winner")) {
                            winner = (Teams)reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("hero")) {
                            heroID = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("stats_recorded")) {
                            statsRecorded = reader.GetBoolean(i);
                        }
                        else if (reader.GetName(i).Equals("mmr_adjustment")) {
                            mmr_adjustment = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("streak_bonus")) {
                            streak_bonus = reader.GetInt32(i);
                        }
                        else if (reader.GetName(i).Equals("team")) {
                            team = (Teams)reader.GetInt32(i);
                        }
                    }
                    Match matchResult = null;
                    foreach (var m in matches) {
                        if (m.MatchID == match_id) {
                            matchResult = m;
                        }
                    }
                    if (matchResult == null) {
                        matchResult = new Match(match_id, leagueID, steam_match_id, season, winner, date, duration, new List<MatchPlayerStats>(), statsRecorded);
                        matches.Add(matchResult);
                    }
                    matchResult.PlayerMatchStats.Add(new MatchPlayerStats(matchResult, steam_id, heroID, goals, assist, steals, turnovers, steal_turnover_difference, pickups, passes, passes_received, save_rate, points, possession_time, time_as_goalie, mmr_adjustment, streak_bonus, team, win));

                }
            }
            return matches;
        }

        //TODO: cascade
        public async Task DeleteLeague(League league)
        {
            log.Debug("DeleteLeague");
            MySqlCommand command = new MySqlCommand();
            command.CommandText = "Delete from leagues where league_id = @league_id";
            command.Parameters.AddWithValue("@league_id", league.LeagueID);
            await ExecuteNoQuery(command);
        }
    }
}
