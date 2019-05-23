using System;
using System.Collections.Generic;
using System.Text;

namespace BanjoBotCore.Controller
{
    public interface ILeagueEventListener
    {
        void LobbyClosed(object sender, LobbyEventArgs e);
        void LobbyStarted(object sender, LobbyEventArgs e);
        void LobbyCanceled(object sender, LobbyPlayerEventArgs e);
        void LobbyCreated(object sender, LobbyPlayerEventArgs e);
        void PlayerKicked(object sender, LobbyPlayerEventArgs e);
        void PlayerJoined(object sender, LobbyPlayerEventArgs e);
        void PlayerLeft(object sender, LobbyPlayerEventArgs e);
        void LobbyFull(object sender, LobbyEventArgs e);
        void PlayerVoted(object sender, LobbyVoteEventArgs e);
        void PlayerVotedCancel(object sender, LobbyPlayerEventArgs e);
        void MatchEnded(object sender, MatchEventArgs e);
        void PlayerRegistrationAccepted(object sender, RegistrationEventArgs e);
        void PlayerRegistered(object sender, RegistrationEventArgs e);
       
    }
}
