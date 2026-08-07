using D2ST.Core.Accounts;
using D2ST.Core.Events;
using D2ST.Core.Stats;
using D2ST.Persistence;
using D2ST.Steam.Events;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Steam.Stats;

public sealed class StatsService : IStatsService
{
    private readonly D2stDbContext _db;
    private readonly IEventStream _events;
    private readonly ISessionStore _sessions;

    public StatsService(D2stDbContext db, IEventStream events, ISessionStore sessions)
    {
        _db = db;
        _events = events;
        _sessions = sessions;
    }

    public async Task<UserStats> ReadAsync(uint accountId, CancellationToken cancellationToken = default)
    {
        var stats = await _db.UserStats
            .Where(stat => stat.AccountId == accountId)
            .Select(stat => new StatValue(stat.Name, stat.Data))
            .ToListAsync(cancellationToken);

        var achievements = await _db.UserAchievements
            .Where(achievement => achievement.AccountId == accountId)
            .Select(achievement => new AchievementValue(
                achievement.Name,
                achievement.Earned,
                achievement.Date,
                achievement.Progress,
                achievement.MaxProgress))
            .ToListAsync(cancellationToken);

        return new UserStats(
            SteamAccount.SteamIdFromAccountId(accountId),
            stats,
            achievements,
            _sessions.OnlineAccounts().Count());
    }

    public async Task StoreAsync(
        uint accountId,
        IReadOnlyList<StatValue> stats,
        IReadOnlyList<AchievementValue> achievements,
        CancellationToken cancellationToken = default)
    {
        var steamId = SteamAccount.SteamIdFromAccountId(accountId);

        foreach (var stat in stats.Where(stat => !string.IsNullOrEmpty(stat.Name)))
        {
            var stored = await _db.UserStats.FindAsync([accountId, stat.Name], cancellationToken);
            if (stored is null)
            {
                _db.UserStats.Add(new UserStatEntity { AccountId = accountId, Name = stat.Name, Data = stat.Data });
            }
            else
            {
                stored.Data = stat.Data;
            }

            _events.Publish(accountId, new SteamEvent
            {
                Type = SteamEventTypes.StatsUpdated,
                SteamId = steamId,
                AccountId = accountId,
                StatName = stat.Name,
                StatValue = stat.Data
            });
        }

        foreach (var achievement in achievements.Where(achievement => !string.IsNullOrEmpty(achievement.Name)))
        {
            var stored = await _db.UserAchievements.FindAsync([accountId, achievement.Name], cancellationToken);
            if (stored is null)
            {
                _db.UserAchievements.Add(new UserAchievementEntity
                {
                    AccountId = accountId,
                    Name = achievement.Name,
                    Earned = achievement.Earned,
                    Date = achievement.Date,
                    Progress = achievement.Progress,
                    MaxProgress = achievement.MaxProgress
                });
            }
            else
            {
                stored.Earned = achievement.Earned;
                stored.Date = achievement.Date;
                stored.Progress = achievement.Progress;
                stored.MaxProgress = achievement.MaxProgress;
            }

            _events.Publish(accountId, new SteamEvent
            {
                Type = SteamEventTypes.AchievementUnlocked,
                SteamId = steamId,
                AccountId = accountId,
                AchievementName = achievement.Name,
                AchievementEarned = achievement.Earned,
                AchievementProgress = achievement.Progress,
                AchievementMaxProgress = achievement.MaxProgress
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
