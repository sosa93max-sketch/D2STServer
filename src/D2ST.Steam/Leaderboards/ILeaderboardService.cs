using D2ST.Core.Leaderboards;

namespace D2ST.Steam.Leaderboards;

public interface ILeaderboardService
{
    Task<Leaderboard> FindOrCreateAsync(
        uint appId,
        string name,
        int sortMethod,
        int displayType,
        CancellationToken cancellationToken = default);

    Task<Leaderboard?> FindAsync(ulong leaderboardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Entries by global rank. <paramref name="users"/> restricts the result to
    /// those players (Steam's "friends" data request), ignoring the range.
    /// </summary>
    Task<LeaderboardEntries?> EntriesAsync(
        ulong leaderboardId,
        int rangeStart,
        int rangeEnd,
        IReadOnlyCollection<ulong> users,
        CancellationToken cancellationToken = default);

    Task<ScoreUploadResult?> UploadAsync(
        ulong leaderboardId,
        uint accountId,
        int uploadMethod,
        int score,
        IReadOnlyList<int> details,
        CancellationToken cancellationToken = default);
}
