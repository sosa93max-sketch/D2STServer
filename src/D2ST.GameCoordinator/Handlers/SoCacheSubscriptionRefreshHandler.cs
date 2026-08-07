using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.SharedObjects;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Re-publishes a subscribed SO cache. The client sends this whenever it
/// suspects its cache is stale (reconnect, or an update it did not see), and it
/// keeps the GC-backed UI disabled until the cache comes back.
/// </summary>
public sealed class SoCacheSubscriptionRefreshHandler : IGcMessageHandler
{
    private readonly WelcomeBuilder _welcome;
    private readonly SoCacheService _soCache;

    public SoCacheSubscriptionRefreshHandler(WelcomeBuilder welcome, SoCacheService soCache)
    {
        _welcome = welcome;
        _soCache = soCache;
    }

    public uint MessageType => GcMsg.SoCacheSubscriptionRefresh;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var refresh = context.Codec.Decode<CMsgSOCacheSubscriptionRefresh>(request.Body);
        var soid = refresh.OwnerSoid;
        var owner = soid is null
            ? SoOwner.ForSteamId(context.SteamId)
            : new SoOwner(soid.Type, soid.Id);

        // A refresh names the cache owner. The caller may only refresh its own
        // account caches and the shared caches (party, lobby) it is subscribed
        // to; anything else is answered with nothing rather than with someone
        // else's objects.
        IReadOnlyList<CMsgSOCacheSubscribed> caches = owner == SoOwner.ForSteamId(context.SteamId)
            ? _welcome.Subscribe(context)
            : _soCache.IsSubscriber(owner, context.AccountId)
                ? _soCache.SnapshotOwner(owner)
                : [];

        return caches
            .Select(cache => new GcMessage(GcMsg.SoCacheSubscribed, context.Codec.Encode(cache)))
            .ToList();
    }
}
