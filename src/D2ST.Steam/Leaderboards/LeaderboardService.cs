using D2ST.Core.Accounts;
using D2ST.Core.Leaderboards;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Steam.Leaderboards;

public sealed class LeaderboardService : ILeaderboardService
{
    private readonly D2stDbContext _db;
    private readonly TimeProvider _time;

    public LeaderboardService(D2stDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<Leaderboard> FindOrCreateAsync(
        uint appId,
        string name,
        int sortMethod,
        int displayType,
        CancellationToken cancellationToken = default)
    {
        var stored = await _db.Leaderboards
            .FirstOrDefaultAsync(leaderboard => leaderboard.AppId == appId && leaderboard.Name == name, cancellationToken);

        if (stored is null)
        {
            stored = new LeaderboardEntity
            {
                AppId = appId,
                Name = name,
                SortMethod = sortMethod,
                DisplayType = displayType
            };

            _db.Leaderboards.Add(stored);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await ToLeaderboardAsync(stored, cancellationToken);
    }

    public async Task<Leaderboard?> FindAsync(ulong leaderboardId, CancellationToken cancellationToken = default)
    {
        var stored = await _db.Leaderboards.FindAsync([(int)leaderboardId], cancellationToken);
        return stored is null ? null : await ToLeaderboardAsync(stored, cancellationToken);
    }

    public async Task<LeaderboardEntries?> EntriesAsync(
        ulong leaderboardId,
        int rangeStart,
        int rangeEnd,
        IReadOnlyCollection<ulong> users,
        CancellationToken cancellationToken = default)
    {
        var stored = await _db.Leaderboards.FindAsync([(int)leaderboardId], cancellationToken);
        if (stored is null)
        {
            return null;
        }

        var ranked = await RankedAsync(stored, cancellationToken);
        var selected = users.Count > 0
            ? ranked.Where(entry => users.Contains(entry.SteamId)).ToList()
            : Slice(ranked, rangeStart, rangeEnd);

        return new LeaderboardEntries(await ToLeaderboardAsync(stored, cancellationToken), selected);
    }

    public async Task<ScoreUploadResult?> UploadAsync(
        ulong leaderboardId,
        uint accountId,
        int uploadMethod,
        int score,
        IReadOnlyList<int> details,
        CancellationToken cancellationToken = default)
    {
        var leaderboard = await _db.Leaderboards.FindAsync([(int)leaderboardId], cancellationToken);
        if (leaderboard is null)
        {
            return null;
        }

        var previousRank = (await RankedAsync(leaderboard, cancellationToken))
            .FirstOrDefault(entry => entry.SteamId == SteamAccount.SteamIdFromAccountId(accountId))?.GlobalRank ?? 0;

        var entry = await _db.LeaderboardEntries.FindAsync([leaderboard.Id, accountId], cancellationToken);
        var improves = entry is null ||
            uploadMethod == LeaderboardMethods.UploadForceUpdate ||
            IsBetter(score, entry.Score, leaderboard.SortMethod);

        if (entry is null)
        {
            entry = new LeaderboardEntryEntity { LeaderboardId = leaderboard.Id, AccountId = accountId };
            _db.LeaderboardEntries.Add(entry);
        }

        if (improves)
        {
            entry.Score = score;
            entry.Details = string.Join(',', details);
        }

        entry.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);

        var newRank = (await RankedAsync(leaderboard, cancellationToken))
            .FirstOrDefault(ranked => ranked.SteamId == SteamAccount.SteamIdFromAccountId(accountId))?.GlobalRank ?? 0;

        return new ScoreUploadResult(true, improves, entry.Score, newRank, previousRank);
    }

    private static bool IsBetter(int score, int currentScore, int sortMethod) =>
        sortMethod == LeaderboardMethods.SortAscending ? score < currentScore : score > currentScore;

    private static List<LeaderboardEntry> Slice(List<LeaderboardEntry> ranked, int rangeStart, int rangeEnd)
    {
        // Steam's range is 1-based and inclusive; an empty range means "all".
        var start = rangeStart <= 0 ? 1 : rangeStart;
        var end = rangeEnd <= 0 ? ranked.Count : Math.Min(rangeEnd, ranked.Count);
        return start > end ? [] : ranked.GetRange(start - 1, end - start + 1);
    }

    private async Task<List<LeaderboardEntry>> RankedAsync(LeaderboardEntity leaderboard, CancellationToken cancellationToken)
    {
        var entries = await _db.LeaderboardEntries
            .Where(entry => entry.LeaderboardId == leaderboard.Id)
            .ToListAsync(cancellationToken);

        var ordered = leaderboard.SortMethod == LeaderboardMethods.SortAscending
            ? entries.OrderBy(entry => entry.Score).ThenBy(entry => entry.UpdatedAt)
            : entries.OrderByDescending(entry => entry.Score).ThenBy(entry => entry.UpdatedAt);

        return ordered
            .Select((entry, index) => new LeaderboardEntry(
                SteamAccount.SteamIdFromAccountId(entry.AccountId),
                index + 1,
                entry.Score,
                ParseDetails(entry.Details),
                entry.UgcHandle))
            .ToList();
    }

    private static IReadOnlyList<int> ParseDetails(string details) => details.Length == 0
        ? []
        : details.Split(',').Select(int.Parse).ToList();

    private async Task<Leaderboard> ToLeaderboardAsync(LeaderboardEntity entity, CancellationToken cancellationToken) => new(
        (ulong)entity.Id,
        entity.AppId,
        entity.Name,
        entity.SortMethod,
        entity.DisplayType,
        await _db.LeaderboardEntries.CountAsync(entry => entry.LeaderboardId == entity.Id, cancellationToken));
}
