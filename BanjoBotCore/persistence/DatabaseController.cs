using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.DependencyInjection;
using BanjoBotCore.persistence;

namespace BanjoBotCore
{
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

        public async Task UpdateMatchResult(MatchResult game)
        {
            log.Debug("UpdateMatchResult");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "update matches set duration=@duration, date=@date, winner=@winner, steam_match_id=@steam_match_id, stats_recorded=@stats_recorded where match_id=@match_id";
            command.Parameters.AddWithValue("@duration", game.Duration);
            command.Parameters.AddWithValue("@date", game.Date);
            command.Parameters.AddWithValue("@winner", game.Winner);
            command.Parameters.AddWithValue("@steam_match_id", game.SteamMatchID);
            command.Parameters.AddWithValue("@match_id", game.MatchID);
            command.Parameters.AddWithValue("@stats_recorded", game.StatsRecorded);
            await ExecuteNoQuery(command);

            StringBuilder queryBuilder =
                new StringBuilder(
                    "REPLACE INTO match_player_stats (steam_id, match_id,hero_id, goals, assist, steals, turnovers, steal_turnover_difference,pickups,passes, passes_received, save_rate, points, possession_time, time_as_goalie, win, mmr_adjustment, streak_bonus,team) VALUES ");
            for (int i = 0; i < game.PlayerMatchStats.Count; i++)
            {
                queryBuilder.AppendFormat(
                    "(@steam_id{0},@match_id{0},@hero_id{0},@goals{0},@assist{0},@steals{0},@turnovers{0},@steal_turnover_difference{0},@pickups{0},@passes{0},@passes_received{0},@save_rate{0},@points{0},@possession_time{0},@time_as_goalie{0},@win{0},@mmr_adjustment{0},@streak_bonus{0},@team{0}),",
                    i);
                if (i == game.PlayerMatchStats.Count - 1)
                {
                    queryBuilder.Replace(',', ';', queryBuilder.Length - 1, 1);
                }
            }

            command = new MySqlCommand(queryBuilder.ToString());

            for (int i = 0; i < game.PlayerMatchStats.Count; i++)
            {
                command.Parameters.AddWithValue("@steam_id" + i, game.PlayerMatchStats[i].SteamID);
                command.Parameters.AddWithValue("@match_id" + i, game.MatchID);
                command.Parameters.AddWithValue("@hero_id" + i, game.PlayerMatchStats[i].HeroID);
                command.Parameters.AddWithValue("@goals" + i, game.PlayerMatchStats[i].Goals);
                command.Parameters.AddWithValue("@assist" + i, game.PlayerMatchStats[i].Assist);
                command.Parameters.AddWithValue("@steals" + i, game.PlayerMatchStats[i].Steals);
                command.Parameters.AddWithValue("@turnovers" + i, game.PlayerMatchStats[i].Turnovers);
                command.Parameters.AddWithValue("@steal_turnover_difference" + i, game.PlayerMatchStats[i].StealTurnDif);
                command.Parameters.AddWithValue("@pickups" + i, game.PlayerMatchStats[i].Pickups);
                command.Parameters.AddWithValue("@passes" + i, game.PlayerMatchStats[i].Passes);
                command.Parameters.AddWithValue("@passes_received" + i, game.PlayerMatchStats[i].PassesReceived);
                command.Parameters.AddWithValue("@save_rate" + i, game.PlayerMatchStats[i].SaveRate);
                command.Parameters.AddWithValue("@points" + i, game.PlayerMatchStats[i].Points);
                command.Parameters.AddWithValue("@possession_time" + i, game.PlayerMatchStats[i].PossessionTime);
                command.Parameters.AddWithValue("@time_as_goalie" + i, game.PlayerMatchStats[i].TimeAsGoalie);
                command.Parameters.AddWithValue("@win" + i, game.PlayerMatchStats[i].Win);
                command.Parameters.AddWithValue("@mmr_adjustment" + i, game.PlayerMatchStats[i].MmrAdjustment);
                command.Parameters.AddWithValue("@streak_bonus" + i, game.PlayerMatchStats[i].StreakBonus);
                command.Parameters.AddWithValue("@team" + i, game.PlayerMatchStats[i].Team);

            }

            await ExecuteNoQuery(command);

        }
         
        public async Task DrawMatch(int matchID)
        {
            log.Debug("DrawMatch");
            MySqlCommand command = new MySqlCommand();
            command.CommandText = "update matches set winner=@winner where match_id=@match_id";
            command.Parameters.AddWithValue("@winner", Teams.Draw);
            command.Parameters.AddWithValue("@match_id", matchID);
            await ExecuteNoQuery(command);

            command = new MySqlCommand();
            command.CommandText = "Delete from match_player_stats where match_id=@match_id";
            command.Parameters.AddWithValue("@match_id", matchID);
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

        public async Task<int> InsertMatch(int leagueID, int season, List<Player> blueteam, List<Player> redteam)
        {
            log.Debug("InsertNewMatch");
            MySqlCommand command = new MySqlCommand();
            command.CommandText =
                "Insert into matches (season, league_id, date) Values (@season,@league_id,@date); SELECT LAST_INSERT_ID()";
            command.Parameters.AddWithValue("@season", season);
            command.Parameters.AddWithValue("@league_id", leagueID);
            command.Parameters.AddWithValue("@date", DateTime.Now);
            int matchID = await ExecuteScalar(command);

            StringBuilder queryBuilder = new StringBuilder("Insert into match_player_stats (steam_id,match_id, team) VALUES ");
            List<Player> allPlayers = new List<Player>();
            allPlayers.AddRange(blueteam);
            allPlayers.AddRange(redteam);
            for (int i = 0; i < allPlayers.Count; i++)
            {
                queryBuilder.AppendFormat("(@steam_id{0},@match_id{0},@team{0}),", i);
                if (i == allPlayers.Count - 1)
                {
                    queryBuilder.Replace(',', ';', queryBuilder.Length - 1, 1);
                }
            }

            command = new MySqlCommand(queryBuilder.ToString());

            for (int i = 0; i < allPlayers.Count; i++)
            {
                Teams team = blueteam.Contains(allPlayers[i]) ? Teams.Blue : Teams.Red;
                command.Parameters.AddWithValue("@steam_id" + i, allPlayers[i].SteamID);
                command.Parameters.AddWithValue("@match_id" + i, matchID);
                command.Parameters.AddWithValue("@team" + i, team);
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

        public async Task<List<Player>> GetPlayerBase(LeagueCoordinator server)
        {
            log.Debug("GetPlayerBase");
            List<Player> result = new List<Player>();
            MySqlCommand command = new MySqlCommand();
            int leagueCount = server.LeagueControllers.Count;
            var params1 = new string[leagueCount];
            for (int i = 0; i < leagueCount; i++)
            {
                params1[i] = string.Format("@leagueid{0}", i);
                command.Parameters.AddWithValue(params1[i], server.LeagueControllers[i].League.LeagueID);
            }

            command.CommandText = string.Format("Select * from players p " +
                                                "inner join players_leagues pl on p.steam_id = pl.steam_id " +
                                                "inner join player_stats ps on p.steam_id = ps.steam_id and pl.league_id=ps.league_id " +
                                                "where pl.league_id IN ({0}) " +
                                                "AND pl.approved = 1", string.Join(", ", params1));

   
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

        public async Task<List<Player>> GetApplicants(LeagueCoordinator server, League league)
        {
            log.Debug("GetApplicants");
            List<Player> result = new List<Player>();
            MySqlCommand command = new MySqlCommand();
            command.CommandText = "Select * from players p " +
                                  "inner join players_leagues pl on p.steam_id = pl.steam_id " +
                                  "where pl.league_id = @league_id " +
                                  "AND pl.approved = 0";

            command.Parameters.AddWithValue("@league_id", league.LeagueID);

            using (MySqlDataReader reader = await ExecuteReader(command))
            {
                while (reader.Read()) {
                    ulong discordId = 0;
                    int leagueId = 0;
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

        public async Task<List<MatchResult>> GetMatchHistory(int leagueID)
        {
            log.Debug("GetMatchHistory");
            List<MatchResult> matches = new List<MatchResult>();
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
                            team = reader.GetInt32(i) == 1 ? Teams.Red : Teams.Blue;
                        }
                    }
                    MatchResult matchResult = null;
                    foreach (var m in matches) {
                        if (m.MatchID == match_id) {
                            matchResult = m;
                        }
                    }
                    if (matchResult == null) {
                        matchResult = new MatchResult(match_id, leagueID, steam_match_id, season, winner, date, duration, new List<MatchPlayerStats>(), statsRecorded);
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
