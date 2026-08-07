using D2ST.Core.Stats;

namespace D2ST.Steam.Stats;

/// <summary>Per-account stats and achievements, as stored by the game.</summary>
public interface IStatsService
{
    Task<UserStats> ReadAsync(uint accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts what the caller sent. Values absent from the request are left
    /// alone: a client stores the stats it touched, not the whole set.
    /// </summary>
    Task StoreAsync(
        uint accountId,
        IReadOnlyList<StatValue> stats,
        IReadOnlyList<AchievementValue> achievements,
        CancellationToken cancellationToken = default);
}
