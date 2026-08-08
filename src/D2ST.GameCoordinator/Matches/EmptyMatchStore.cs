using D2ST.Core.Matches;

namespace D2ST.GameCoordinator.Matches;

/// <summary>
/// Keeps the reusable GC host usable without a persistence adapter. The API
/// host replaces this registration with its SQLite-backed implementation.
/// </summary>
internal sealed class EmptyMatchStore : IMatchStore
{
    public MatchRecordResult Record(MatchRecord match) => new(false);

    public IReadOnlyList<PlayerMatchHistoryEntry> GetPlayerMatchHistory(
        uint accountId,
        ulong startAtMatchId,
        uint matchesRequested,
        int heroId,
        bool includePracticeMatches,
        bool includeCustomGames,
        bool includeEventGames) => [];

    public IReadOnlyList<MatchMinimalRecord> GetMatchesMinimal(IReadOnlyList<ulong> matchIds) => [];

    public IReadOnlyList<TeammateStatRecord> GetTeammateStats(uint accountId) => [];

    public PlayerProfileStats GetProfileStats(uint accountId) =>
        PlayerProfileStats.Empty(accountId);

    public IReadOnlyList<HeroStatsRecord> GetHeroStandings(uint accountId) => [];

    public IReadOnlyList<HeroStatsRecord> GetHeroStats(uint accountId) => [];

    public IReadOnlyList<int> GetHeroOrder() => [];
}
