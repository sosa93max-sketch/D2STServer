using D2ST.Core.Matches;

namespace D2ST.GameCoordinator.Matches;

/// <summary>
/// Persistence boundary for finished local matches. The GC stays independent
/// from EF Core; the API host provides the implementation.
/// </summary>
public interface IMatchStore
{
    /// <summary>
    /// Stores a match and updates its player aggregates. The result is false
    /// when the match id was already processed, which makes repeated 7004
    /// messages harmless.
    /// </summary>
    MatchRecordResult Record(MatchRecord match);

    /// <summary>Reads the account's history in newest-first pages.</summary>
    IReadOnlyList<PlayerMatchHistoryEntry> GetPlayerMatchHistory(
        uint accountId,
        ulong startAtMatchId,
        uint matchesRequested,
        int heroId,
        bool includePracticeMatches,
        bool includeCustomGames,
        bool includeEventGames);

    /// <summary>Reads compact projections for a requested set of match ids.</summary>
    IReadOnlyList<MatchMinimalRecord> GetMatchesMinimal(IReadOnlyList<ulong> matchIds);

    /// <summary>Aggregates players who shared a team with the account.</summary>
    IReadOnlyList<TeammateStatRecord> GetTeammateStats(uint accountId);

    /// <summary>Reads the current profile projection for the account.</summary>
    PlayerProfileStats GetProfileStats(uint accountId);
}

public sealed record MatchRecordResult(bool Created);
