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
}
