using System.Text;
using D2ST.Core.Accounts;
using D2ST.Core.Events;
using D2ST.Core.Steam;
using D2ST.Steam.Events;

namespace D2ST.Steam.Invites;

public sealed class GameInviteService : IGameInviteService
{
    private readonly IEventStream _events;

    public GameInviteService(IEventStream events)
    {
        _events = events;
    }

    public bool Invite(SteamSession session, ulong inviteeSteamId, string connectString)
    {
        if (inviteeSteamId == 0 || string.IsNullOrWhiteSpace(connectString))
        {
            return false;
        }

        _events.Publish(SteamAccount.AccountIdFromSteamId(inviteeSteamId), new SteamEvent
        {
            Type = SteamEventTypes.GameInvite,
            SteamId = session.Account.SteamId,
            AccountId = session.Account.AccountId,
            PersonaName = session.PersonaName ?? string.Empty,
            AppId = session.AppId,
            // The connect string is opaque to the server and may hold anything
            // the game puts in a +connect argument, so it travels base64-encoded.
            PayloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(connectString))
        });

        return true;
    }
}
