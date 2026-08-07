using D2ST.Core.Steam;

namespace D2ST.Steam.Invites;

/// <summary>
/// "Join my game" invites carrying a connect string, as opposed to lobby
/// invites which carry a lobby id.
/// </summary>
public interface IGameInviteService
{
    bool Invite(SteamSession session, ulong inviteeSteamId, string connectString);
}
