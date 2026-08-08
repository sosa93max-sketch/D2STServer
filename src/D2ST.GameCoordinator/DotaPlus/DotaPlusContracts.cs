namespace D2ST.GameCoordinator.DotaPlus;

/// <summary>
/// Local Dota Plus entitlement projected by the server. It deliberately does
/// not model Valve billing: the LAN server owns the subscription lifetime.
/// </summary>
public sealed record DotaPlusState(
    uint AccountId,
    bool Enabled,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ExpiresAt,
    uint PlusFlags,
    ulong SteamAgreementId)
{
    public static DotaPlusState Inactive(uint accountId) =>
        new(accountId, false, null, null, 0, 0);

    public bool IsActiveAt(DateTimeOffset now) =>
        Enabled && (ExpiresAt is null || ExpiresAt > now);

    public int DaysRemainingAt(DateTimeOffset now)
    {
        if (!IsActiveAt(now) || ExpiresAt is null)
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, Math.Ceiling((ExpiresAt.Value - now).TotalDays));
    }
}

public sealed record DotaPlusMutationResult(
    bool Success,
    string Code,
    string Message,
    DotaPlusState State);

/// <summary>
/// One locally-owned Dota Plus challenge. The fields mirror the 2010 shared
/// object so the client can render and update the same projection it receives
/// from a normal GC.
/// </summary>
public sealed record DotaPlusChallenge(
    uint AccountId,
    uint EventId,
    uint SlotId,
    uint IntParam0,
    uint IntParam1,
    DateTimeOffset CreatedAt,
    uint Completed,
    uint SequenceId,
    uint ChallengeTier,
    uint Flags,
    uint Attempts,
    uint CompleteLimit,
    uint QuestRank,
    uint MaxQuestRank,
    uint InstanceId,
    int HeroId,
    uint TemplateId)
{
    public uint Target => IntParam0;
}

public sealed record DotaPlusSnapshot(
    uint AccountId,
    bool Active,
    long Shards,
    IReadOnlyList<DotaPlusChallenge> Challenges);

public sealed record DotaPlusChallengeReport(
    uint SlotId,
    uint SequenceId,
    uint Progress,
    uint ChallengeRank);

public sealed record DotaPlusProgressResult(
    bool Success,
    string Code,
    string Message,
    DotaPlusSnapshot Snapshot,
    long ShardsEarned);

public sealed record DotaPlusRerollResult(
    bool Success,
    string Code,
    string Message,
    DotaPlusSnapshot Snapshot);

public sealed record DotaPlusRelicResult(
    bool Success,
    string Code,
    string Message,
    DotaPlusSnapshot Snapshot,
    uint KillEaterType);

public sealed record DotaPlusShardMutationResult(
    bool Success,
    string Code,
    string Message,
    DotaPlusSnapshot Snapshot);

/// <summary>Persistence boundary used by the GC without coupling it to EF Core.</summary>
public interface IDotaPlusStore
{
    DotaPlusState Get(uint accountId);

    IReadOnlyDictionary<uint, DotaPlusState> GetMany(IReadOnlyCollection<uint> accountIds);

    DotaPlusMutationResult UpdateSubscription(
        uint accountId,
        bool enabled,
        int days,
        bool extend,
        uint changedByAccountId,
        string? reason);

    DotaPlusSnapshot GetSnapshot(uint accountId);

    IReadOnlyDictionary<uint, DotaPlusSnapshot> GetManySnapshots(IReadOnlyCollection<uint> accountIds);

    DotaPlusSnapshot EnsureChallenges(uint accountId);

    DotaPlusProgressResult ApplyMatchProgress(
        uint accountId,
        ulong matchId,
        int heroId,
        bool won,
        uint kills,
        uint durationSeconds);

    DotaPlusProgressResult ApplyChallengeReport(
        uint accountId,
        ulong matchId,
        int heroId,
        IReadOnlyList<DotaPlusChallengeReport> reports);

    DotaPlusRerollResult RerollChallenge(
        uint accountId,
        uint sequenceId,
        int heroId);

    DotaPlusRelicResult PurchaseRelic(
        uint accountId,
        int heroId,
        int rarity);

    DotaPlusShardMutationResult AdjustShards(
        uint accountId,
        long delta,
        uint changedByAccountId,
        string? reason);
}

/// <summary>
/// Keeps the reusable GameCoordinator assembly usable without the API host.
/// </summary>
internal sealed class EmptyDotaPlusStore : IDotaPlusStore
{
    public DotaPlusState Get(uint accountId) => DotaPlusState.Inactive(accountId);

    public IReadOnlyDictionary<uint, DotaPlusState> GetMany(IReadOnlyCollection<uint> accountIds) =>
        accountIds.Distinct().ToDictionary(id => id, DotaPlusState.Inactive);

    public DotaPlusMutationResult UpdateSubscription(
        uint accountId,
        bool enabled,
        int days,
        bool extend,
        uint changedByAccountId,
        string? reason) =>
        new(
            false,
            "not_configured",
            "La persistencia de Dota Plus no está configurada en este host.",
            DotaPlusState.Inactive(accountId));

    public DotaPlusSnapshot GetSnapshot(uint accountId) =>
        new(accountId, false, 0, []);

    public IReadOnlyDictionary<uint, DotaPlusSnapshot> GetManySnapshots(
        IReadOnlyCollection<uint> accountIds) =>
        accountIds.Distinct().ToDictionary(id => id, GetSnapshot);

    public DotaPlusSnapshot EnsureChallenges(uint accountId) => GetSnapshot(accountId);

    public DotaPlusProgressResult ApplyMatchProgress(
        uint accountId,
        ulong matchId,
        int heroId,
        bool won,
        uint kills,
        uint durationSeconds) =>
        new(false, "not_configured", "La persistencia de Dota Plus no está configurada en este host.", GetSnapshot(accountId), 0);

    public DotaPlusProgressResult ApplyChallengeReport(
        uint accountId,
        ulong matchId,
        int heroId,
        IReadOnlyList<DotaPlusChallengeReport> reports) =>
        new(false, "not_configured", "La persistencia de Dota Plus no está configurada en este host.", GetSnapshot(accountId), 0);

    public DotaPlusRerollResult RerollChallenge(
        uint accountId,
        uint sequenceId,
        int heroId) =>
        new(false, "not_configured", "La persistencia de Dota Plus no está configurada en este host.", GetSnapshot(accountId));

    public DotaPlusRelicResult PurchaseRelic(
        uint accountId,
        int heroId,
        int rarity) =>
        new(false, "not_configured", "La persistencia de Dota Plus no está configurada en este host.", GetSnapshot(accountId), 0);

    public DotaPlusShardMutationResult AdjustShards(
        uint accountId,
        long delta,
        uint changedByAccountId,
        string? reason) =>
        new(false, "not_configured", "La persistencia de Dota Plus no está configurada en este host.", GetSnapshot(accountId));
}
