using D2ST.Core.Accounts;
using D2ST.GameCoordinator.SharedObjects;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.DotaPlus;

/// <summary>
/// Converts the local entitlement into the Dota Plus account Shared Object and
/// publishes a delta when an administrator changes a connected account.
/// </summary>
public sealed class DotaPlusProjection
{
    private readonly IDotaPlusStore _store;
    private readonly SoCacheService _soCache;
    private readonly TimeProvider _time;

    public DotaPlusProjection(
        IDotaPlusStore store,
        SoCacheService soCache,
        TimeProvider time)
    {
        _store = store;
        _soCache = soCache;
        _time = time;
    }

    public bool IsActive(uint accountId) =>
        _store.Get(accountId).IsActiveAt(_time.GetUtcNow());

    public CSODOTAGameAccountPlus Build(uint accountId) =>
        Build(_store.Get(accountId), _time.GetUtcNow());

    public bool Refresh(uint accountId)
    {
        var steamId = SteamAccount.SteamIdFromAccountId(accountId);
        return _soCache.SetIfChanged(
            SoCacheKey.Game(steamId),
            new SoObjectKey(DotaSoCache.TypeDotaGameAccountPlus, accountId),
            Build(accountId));
    }

    private static CSODOTAGameAccountPlus Build(DotaPlusState state, DateTimeOffset now)
    {
        var active = state.IsActiveAt(now);
        var startedAt = ToUnixSeconds(state.StartedAt);
        var expiresAt = ToUnixSeconds(state.ExpiresAt);
        var remaining = active && state.ExpiresAt is not null
            ? Math.Max(0, state.ExpiresAt.Value.ToUnixTimeSeconds() - now.ToUnixTimeSeconds())
            : 0;

        return new CSODOTAGameAccountPlus
        {
            AccountId = state.AccountId,
            OriginalStartDate = startedAt,
            PlusFlags = active ? state.PlusFlags : 0,
            PlusStatus = active ? 1u : 0u,
            PrepaidTimeStart = startedAt,
            PrepaidTimeBalance = ToUInt32(remaining),
            NextPaymentDate = active ? expiresAt : 0,
            SteamAgreementId = state.SteamAgreementId
        };
    }

    private static uint ToUnixSeconds(DateTimeOffset? value)
    {
        if (value is null)
        {
            return 0;
        }

        return ToUInt32(Math.Max(0, value.Value.ToUnixTimeSeconds()));
    }

    private static uint ToUInt32(long value) =>
        value <= 0 ? 0 : value >= uint.MaxValue ? uint.MaxValue : (uint)value;
}
