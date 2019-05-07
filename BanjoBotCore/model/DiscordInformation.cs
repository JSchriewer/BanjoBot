using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace BanjoBot
{
    public class DiscordInformation
    {
        public SocketGuild DiscordServer { get; set; }
        public ulong DiscordServerId { get; }
        private ulong channel_ID;
        private ulong moderatorRole_ID;
        private ulong league_role_ID;
        private ulong moderatorChannel_ID;
        public bool AutoAccept { get; set; } = true;
        public bool NeedSteamToRegister { get; set; } = true;

        public DiscordInformation(ulong discordServer, ulong channel, ulong moderatorRole, ulong leagueRole, ulong moderatorChannel, bool autoaccept, bool steamRegister)
        {
            DiscordServerId = discordServer;
            channel_ID = channel;
            moderatorRole_ID = moderatorRole;
            league_role_ID = leagueRole;
            moderatorChannel_ID = moderatorChannel;
            AutoAccept = autoaccept;
            NeedSteamToRegister = steamRegister;
        }

        public DiscordInformation(ulong discordServerID, SocketGuild discordServer, ulong channelID) {
            DiscordServerId = discordServerID;
            DiscordServer = discordServer;
            channel_ID = channelID;
            moderatorRole_ID = ulong.MinValue;
            league_role_ID = ulong.MinValue;
            moderatorChannel_ID = ulong.MinValue;
            AutoAccept = false;
            NeedSteamToRegister = true;
        }

        public SocketGuildChannel Channel
        {
            get { return DiscordServer.GetChannel(channel_ID); }
            set { channel_ID = value.Id; }
        }

        public SocketGuildChannel ModeratorChannel {
            get
            {
                if (moderatorChannel_ID != ulong.MinValue)
                    return DiscordServer.GetChannel(moderatorChannel_ID);
                else
                    return null;
            }
            set { moderatorChannel_ID = value.Id; }
        }


        public SocketRole ModeratorRole
        {
            get
            {
                if (moderatorRole_ID != ulong.MinValue)
                    return DiscordServer.GetRole(moderatorRole_ID);
                else
                    return null;
            }
            set { moderatorRole_ID = value.Id; }
        }

        public SocketRole LeagueRole
        {
            get
            {
                if (league_role_ID != ulong.MinValue)
                    return DiscordServer.GetRole(league_role_ID);
                else
                    return null;
               
            }
            set { league_role_ID = value.Id; }
        }

        
    }
}
