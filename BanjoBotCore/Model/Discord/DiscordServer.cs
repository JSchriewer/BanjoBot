using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace BanjoBotCore.Model
{
    public class DiscordServer
    {
        public SocketGuild SocketGuild { get; }
        public bool AutoAccept { get; set; } = true;
        public bool NeedSteamToRegister { get; set; } = true;
        public SocketRole RegistrationRole { get; set; }
        public SocketGuildChannel ModeratorChannel { get; set; }

        public DiscordServer(SocketGuild socketGuild, bool autoAccept, bool needSteamRegister, ulong moderatorChannelID, ulong registrationRoleID)
        {
            SocketGuild = socketGuild;
            AutoAccept = autoAccept;
            NeedSteamToRegister = needSteamRegister;
            ModeratorChannel = socketGuild.GetChannel(moderatorChannelID);
            RegistrationRole = socketGuild.GetRole(registrationRoleID);
        }

        public DiscordServer(SocketGuild socketGuild)
        {
            SocketGuild = socketGuild;
            AutoAccept = false;
            NeedSteamToRegister = true;
        }
    }
}
