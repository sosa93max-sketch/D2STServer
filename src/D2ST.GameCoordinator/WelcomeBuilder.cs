using D2ST.GameCoordinator.SharedObjects;
using D2ST.GameCoordinator.Econ;
using D2ST.GameCoordinator.Matches;
using D2ST.GameCoordinator.Profiles;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator;

/// <summary>
/// Builds the CMsgClientWelcome payload, including the Shared Object caches the
/// client subscribes to at logon. Dota only unlocks GC-backed UI once it has an
/// account cache (service 0) and an econ cache (service 1) owned by its own
/// Steam id, so both are always published even when they are still empty. The
/// caches owned by somebody else that the player is nonetheless part of (its
/// party) come from the registered <see cref="IGcWelcomeContributor"/>s.
/// <para>
/// The caches are seeded into <see cref="SoCacheService"/> rather than built
/// inline, so that a later create/update on the same objects (econ, party) is
/// published as a delta against exactly what the welcome sent.
/// </para>
/// </summary>
public sealed class WelcomeBuilder
{
    private readonly SoCacheService _soCache;
    private readonly IReadOnlyList<IGcWelcomeContributor> _contributors;
    private readonly IMatchStore _matches;
    private readonly EconInventory _inventory;

    public WelcomeBuilder(
        SoCacheService soCache,
        IEnumerable<IGcWelcomeContributor> contributors,
        IMatchStore matches,
        EconInventory inventory)
    {
        _soCache = soCache;
        _contributors = contributors.ToList();
        _matches = matches;
        _inventory = inventory;
    }

    public CMsgClientWelcome Build(GcContext context)
    {
        var socacheFileVersion = (uint)context.Profile.SocacheFileVersion;

        var welcome = new CMsgClientWelcome
        {
            Version = (uint)context.ClientVersion,
            GcSocacheFileVersion = socacheFileVersion,
            GameData = context.Codec.Encode(new CMsgDOTAWelcome
            {
                GcSocacheFileVersion = socacheFileVersion,
                Allow3rdPartyMatchHistory = true
            })
        };

        welcome.OutofdateSubscribedCaches.AddRange(Subscribe(context));
        return welcome;
    }

    /// <summary>
    /// Reconciles the account's caches with the current projections and
    /// subscribes it to them, returning the subscription messages in welcome
    /// order. A later subscription refresh replays the same caches from the
    /// store, so the two paths cannot drift.
    /// </summary>
    public IReadOnlyList<CMsgSOCacheSubscribed> Subscribe(GcContext context)
    {
        EnsureCaches(context);

        var caches = _soCache.Subscribe(context.AccountId, SoOwner.ForSteamId(context.SteamId)).ToList();
        foreach (var contributor in _contributors)
        {
            caches.AddRange(contributor.CachesFor(context));
        }

        return caches;
    }

    private void EnsureCaches(GcContext context)
    {
        var game = SoCacheKey.Game(context.SteamId);
        var econ = SoCacheKey.Econ(context.SteamId);

        _soCache.SetIfChanged(
            game,
            new SoObjectKey(DotaSoCache.TypeDotaGameAccountClient, context.AccountId),
            AccountSnapshot(context.AccountId));

        // Dota Plus shipped in 2018; older builds have no 2012 cache bucket.
        if (context.Profile.IncludeDotaPlus)
        {
            _soCache.SeedIfAbsent(
                game,
                new SoObjectKey(DotaSoCache.TypeDotaGameAccountPlus, context.AccountId),
                () => new CSODOTAGameAccountPlus { AccountId = context.AccountId });
        }

        _soCache.SeedIfAbsent(
            econ,
            new SoObjectKey(DotaSoCache.TypeEconGameAccountClient, context.AccountId),
            () => new CSOEconGameAccountClient { EligibleForOnlinePlay = true });

        _inventory.EnsureCache(context.SteamId, context.AccountId);
    }

    private CSODOTAGameAccountClient AccountSnapshot(uint accountId)
    {
        var stats = _matches.GetProfileStats(accountId);
        var account = new CSODOTAGameAccountClient
        {
            AccountId = accountId,
            Wins = NonNegative(stats.Wins),
            Losses = NonNegative(stats.Losses),
            CasualGamesPlayed = NonNegative(stats.Games),
            LeaverCount = NonNegative(stats.LeaverCount)
        };

        LocalConductState.ApplyTo(account);
        return account;
    }

    private static uint NonNegative(int value) => (uint)Math.Max(0, value);
}
