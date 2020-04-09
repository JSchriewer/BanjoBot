using Discord.WebSocket;

namespace BanjoBotCore.Model
{
    //TODO: Add reference to Discord Server?
    public class LeagueDiscordConfig
    {
        public SocketGuildChannel LeagueChannel { get; set; }
        public SocketRole ModeratorRole { get; set; }

        public LeagueDiscordConfig(SocketGuildChannel leagueChannel, SocketRole moderatorRole)
        {
            LeagueChannel = leagueChannel;
            ModeratorRole = moderatorRole;
        }

        public LeagueDiscordConfig(SocketGuildChannel leagueChannel)
        {
            LeagueChannel = leagueChannel;
        }
    }
}