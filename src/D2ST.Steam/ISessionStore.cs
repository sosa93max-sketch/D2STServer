using D2ST.Core.Steam;

namespace D2ST.Steam;

/// <summary>
/// Holds the access tokens handed out at logon so later calls (GC exchange and
/// poll) can be tied back to an account instead of trusting whatever account id
/// the caller puts in the request body. It is also the source of truth for
/// presence: a player is online exactly while a live client session exists.
/// </summary>
public interface ISessionStore
{
    void Add(SteamSession session);

    SteamSession? Find(string? token);

    /// <summary>
    /// Finds the most recently used password/web session from an address. The
    /// shim uses this only when its UseActiveWebUser option is enabled.
    /// </summary>
    SteamSession? FindActiveWebSession(string? remoteIp);

    bool Remove(string? token);

    /// <summary>Marks the session as just used, keeping presence alive.</summary>
    void Touch(SteamSession session);

    /// <summary>Whether the account has a client session seen within the presence window.</summary>
    bool IsOnline(uint accountId);

    /// <summary>Accounts currently considered online.</summary>
    IReadOnlyCollection<uint> OnlineAccounts();

    /// <summary>Drops every client session of an account (explicit logoff).</summary>
    int RemoveClientSessions(uint accountId);

    /// <summary>Drops every session of an account (account deletion).</summary>
    int RemoveAll(uint accountId);
}
