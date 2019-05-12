using System;
using System.Collections.Generic;
using System.Text;

namespace BanjoBotCore.Controller
{
    public interface ILeagueEventListener
    {
        void LobbyClosed(object sender, LeagueEventArgs e);

        void LobbyCreated(object sender, LeagueEventArgs e);

        void LobbyChanged(object sender, LeagueEventArgs e);

        void MatchEnded(object sender, MatchEventArgs e);

        void NewApplicant(object sender, RegistrationEventArgs e);
        void AddedPlayerToLeague(object sender, RegistrationEventArgs e);
    }
}
