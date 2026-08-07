using D2ST.Core.Ranking;

namespace D2ST.GameCoordinator.Ranks;

/// <summary>
/// Where the GC reads and writes ratings. Implemented in the host (D2ST.Api),
/// which owns the database; the GC only sees the interface.
/// </summary>
public interface IRankStore
{
    /// <summary>The current rating, creating a Herald-1 row on first sight.</summary>
    PlayerRank GetOrCreate(uint accountId);

    /// <summary>Applies a finished match's results and persists the changes.</summary>
    void ApplyMatchResult(IReadOnlyList<(uint AccountId, bool Won)> results);
}
