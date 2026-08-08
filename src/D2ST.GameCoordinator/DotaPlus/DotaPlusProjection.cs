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
        var changed = _soCache.SetIfChanged(
            SoCacheKey.Game(steamId),
            new SoObjectKey(DotaSoCache.TypeDotaGameAccountPlus, accountId),
            Build(accountId));
        return RefreshChallenges(accountId) || changed;
    }

    /// <summary>
    /// Reconciles the persisted challenge rows with the account game cache.
    /// Rerolls deliberately change the SO key (sequence id), so the old
    /// challenge is destroyed before the replacement is published.
    /// </summary>
    public bool RefreshChallenges(uint accountId)
    {
        var snapshot = _store.EnsureChallenges(accountId);
        var key = SoCacheKey.Game(SteamAccount.SteamIdFromAccountId(accountId));
        var existing = _soCache.ObjectsOfType<CSODOTAPlayerChallenge>(
            key,
            DotaSoCache.TypeDotaPlayerChallenge);
        var currentKeys = snapshot.Active
            ? snapshot.Challenges
                .Select(challenge => new SoObjectKey(
                    DotaSoCache.TypeDotaPlayerChallenge,
                    challenge.SequenceId))
                .ToHashSet()
            : new HashSet<SoObjectKey>();
        var changed = false;

        foreach (var item in existing)
        {
            if (!currentKeys.Contains(item.Key))
            {
                changed |= _soCache.Destroy(key, item.Key);
            }
        }

        if (!snapshot.Active)
        {
            return changed;
        }

        foreach (var challenge in snapshot.Challenges)
        {
            changed |= _soCache.SetIfChanged(
                key,
                new SoObjectKey(
                    DotaSoCache.TypeDotaPlayerChallenge,
                    challenge.SequenceId),
                ToProtocol(challenge));
        }

        return changed;
    }

    private static CSODOTAPlayerChallenge ToProtocol(DotaPlusChallenge challenge) =>
        new()
        {
            AccountId = challenge.AccountId,
            EventId = challenge.EventId,
            SlotId = challenge.SlotId,
            IntParam0 = challenge.IntParam0,
            IntParam1 = challenge.IntParam1,
            CreatedTime = ToUInt32(challenge.CreatedAt.ToUnixTimeSeconds()),
            Completed = challenge.Completed,
            SequenceId = challenge.SequenceId,
            ChallengeTier = challenge.ChallengeTier,
            Flags = challenge.Flags,
            Attempts = challenge.Attempts,
            CompleteLimit = challenge.CompleteLimit,
            QuestRank = challenge.QuestRank,
            MaxQuestRank = challenge.MaxQuestRank,
            InstanceId = challenge.InstanceId,
            HeroId = challenge.HeroId,
            TemplateId = challenge.TemplateId
        };

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
