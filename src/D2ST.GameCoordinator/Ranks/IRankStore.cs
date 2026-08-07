using D2ST.Core.Ranking;

namespace D2ST.GameCoordinator.Ranks;

/// <summary>
/// Where the GC reads and writes ratings. Implemented in the host (D2ST.Api),
/// which owns the database; the GC only sees the interface.
/// </summary>
public interface IRankStore
{
    /// <summary>The current rating, creating an uncalibrated snapshot on first sight.</summary>
    PlayerRank GetOrCreate(uint accountId);

    /// <summary>Applies a finished match's results and persists the changes.</summary>
    void ApplyMatchResult(IReadOnlyList<(uint AccountId, bool Won)> results);

    /// <summary>Adds (or subtracts) MMR by hand from the admin web.</summary>
    PlayerRank Adjust(uint accountId, int delta);

    /// <summary>Resets a player to 0 MMR / uncalibrated with a clean record.</summary>
    PlayerRank Reset(uint accountId);
}
