namespace D2ST.Steam;

/// <summary>Tunables for session lifetime and presence, bound from configuration.</summary>
public sealed class SteamOptions
{
    public const string SectionName = "Steam";

    /// <summary>
    /// How long a session survives without being used. The shim re-runs its
    /// logon handshake whenever a call comes back 401, so expiring is safe.
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How long after its last call a client still counts as online. It has to
    /// outlast the client's own poll interval, or friends flicker offline.
    /// </summary>
    public TimeSpan PresenceTimeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>How often presence is reconciled and offline transitions published.</summary>
    public TimeSpan PresenceSweepInterval { get; set; } = TimeSpan.FromSeconds(15);
}
