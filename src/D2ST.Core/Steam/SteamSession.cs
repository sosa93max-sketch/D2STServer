using D2ST.Core.Accounts;

namespace D2ST.Core.Steam;

/// <summary>
/// An authenticated session bound to a <see cref="SteamAccount"/>. The client
/// build is not known at logon (the shim only learns it once the game sends its
/// GCClientHello), so <see cref="ClientVersion"/> is filled in later and kept
/// here for the lifetime of the session.
/// </summary>
public sealed class SteamSession
{
    public required SteamAccount Account { get; init; }

    public required string Token { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    public uint AppId { get; init; }

    public string? PersonaName { get; set; }

    public int ClientVersion { get; set; }

    /// <summary>
    /// Identifies the machine/process behind the session. Two clients on the
    /// same account (e.g. a second PC) must not be treated as one presence, and
    /// events can be addressed to a single instance.
    /// </summary>
    public string ClientInstanceId { get; init; } = string.Empty;

    /// <summary>"client" or "dedicated": a dedicated server is not a player.</summary>
    public string ProcessRole { get; init; } = ProcessRoles.Client;

    /// <summary>
    /// True for a password-authenticated web/admin session. When the shim is
    /// configured to use the active web user, the server can bind its
    /// passwordless game session to this account instead of creating a second
    /// fallback identity.
    /// </summary>
    public bool IsWebSession { get; set; }

    /// <summary>Network address from which this session was authenticated.</summary>
    public string RemoteIp { get; set; } = string.Empty;

    /// <summary>
    /// Last time the session was used. Presence is derived from it, so every
    /// authenticated request refreshes it.
    /// </summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
