namespace D2ST.GameCoordinator.Players;

/// <summary>
/// Tells the GC whether another player is reachable right now. Only the host
/// knows that (it owns the sessions), and the GC needs it before it addresses a
/// message to somebody who did not ask for it: an invite to a player who is not
/// connected would sit in the push queue until it expired, so the inviter is
/// told "offline" instead.
/// </summary>
public interface IGcPlayerDirectory
{
    bool IsOnline(ulong steamId);
}

/// <summary>
/// Fallback for a host that wires no directory: nobody is reachable, so nothing
/// is ever addressed to another player.
/// </summary>
public sealed class OfflineGcPlayerDirectory : IGcPlayerDirectory
{
    public static readonly OfflineGcPlayerDirectory Instance = new();

    public bool IsOnline(ulong steamId) => false;
}
