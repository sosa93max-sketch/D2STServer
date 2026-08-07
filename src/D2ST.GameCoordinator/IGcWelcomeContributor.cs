using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator;

/// <summary>
/// Adds the shared caches a player belongs to (party, later lobby) to the
/// welcome. They are not owned by the player's Steam id, so
/// <see cref="WelcomeBuilder"/> cannot find them from the account alone, and a
/// client that reconnects mid-party has to be told about them before it can
/// draw the party UI again.
/// </summary>
public interface IGcWelcomeContributor
{
    IReadOnlyList<CMsgSOCacheSubscribed> CachesFor(GcContext context);
}
