using D2ST.Core.Matches;

namespace D2ST.GameCoordinator.Matches;

/// <summary>
/// Keeps the reusable GC host usable without a persistence adapter. The API
/// host replaces this registration with its SQLite-backed implementation.
/// </summary>
internal sealed class EmptyMatchStore : IMatchStore
{
    public MatchRecordResult Record(MatchRecord match) => new(false);

    public PlayerProfileStats GetProfileStats(uint accountId) =>
        PlayerProfileStats.Empty(accountId);
}
