using D2ST.Core.Accounts;
using D2ST.GameCoordinator.Players;
using D2ST.Steam;

namespace D2ST.Api;

/// <summary>
/// Answers the GC's "can I reach this player?" from the session table: a player
/// is reachable while it still has a live client session, which is the same
/// window the presence surface reports online.
/// </summary>
public sealed class SessionGcPlayerDirectory : IGcPlayerDirectory
{
    private readonly ISessionStore _sessions;

    public SessionGcPlayerDirectory(ISessionStore sessions)
    {
        _sessions = sessions;
    }

    public bool IsOnline(ulong steamId) => _sessions.IsOnline(SteamAccount.AccountIdFromSteamId(steamId));
}
