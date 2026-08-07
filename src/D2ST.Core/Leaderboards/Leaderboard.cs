namespace D2ST.Core.Leaderboards;

public sealed record Leaderboard(ulong Id, uint AppId, string Name, int SortMethod, int DisplayType, int EntryCount);

public sealed record LeaderboardEntry(
    ulong SteamId,
    int GlobalRank,
    int Score,
    IReadOnlyList<int> Details,
    ulong UgcHandle);

public sealed record LeaderboardEntries(Leaderboard Leaderboard, IReadOnlyList<LeaderboardEntry> Entries);

public sealed record ScoreUploadResult(
    bool Success,
    bool ScoreChanged,
    int Score,
    int GlobalRankNew,
    int GlobalRankPrevious);

/// <summary>Steam's ELeaderboardSortMethod / ELeaderboardUploadScoreMethod.</summary>
public static class LeaderboardMethods
{
    public const int SortAscending = 1;
    public const int SortDescending = 2;
    public const int UploadKeepBest = 1;
    public const int UploadForceUpdate = 2;
}
