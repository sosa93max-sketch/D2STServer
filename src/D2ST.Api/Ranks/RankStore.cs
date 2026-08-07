using System.Collections.Concurrent;
using D2ST.Core.Ranking;
using D2ST.GameCoordinator.Ranks;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Ranks;

/// <summary>
/// EF-backed implementation of <see cref="IRankStore"/>: ratings live in the
/// PlayerRanks table and are cached in memory per process.
/// </summary>
public sealed class RankStore : IRankStore
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ConcurrentDictionary<uint, PlayerRank> _cache = new();

    public RankStore(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public PlayerRank GetOrCreate(uint accountId)
    {
        if (_cache.TryGetValue(accountId, out var cached))
        {
            return cached;
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var entity = db.PlayerRanks.AsNoTracking()
            .SingleOrDefault(rank => rank.AccountId == accountId);
        var rank = entity is null
            ? new PlayerRank(accountId, 0, 0, 0, 0, false)
            : new PlayerRank(
                accountId,
                entity.Mmr,
                entity.Wins,
                entity.Losses,
                entity.Games,
                entity.IsCalibrated);
        _cache[accountId] = rank;
        return rank;
    }

    public void ApplyMatchResult(IReadOnlyList<(uint AccountId, bool Won)> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        var won = results.ToDictionary(result => result.AccountId, result => result.Won);
        var before = results.ToDictionary(result => result.AccountId, result => GetOrCreate(result.AccountId));
        var winners = before.Where(pair => won[pair.Key]).Select(pair => pair.Value.Mmr).ToList();
        var losers = before.Where(pair => !won[pair.Key]).Select(pair => pair.Value.Mmr).ToList();

        var updates = new List<(PlayerRank Rank, int Delta, bool Won)>();
        foreach (var (accountId, isWinner) in won)
        {
            var opponents = isWinner ? losers : winners;
            if (opponents.Count == 0)
            {
                continue;
            }

            var rank = before[accountId];
            var opponentAverage = (int)Math.Round(opponents.Average());
            updates.Add((rank, RankMath.Delta(rank.Mmr, opponentAverage, isWinner), isWinner));
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var now = DateTimeOffset.UtcNow;
        foreach (var (rank, delta, isWinner) in updates)
        {
            var next = new PlayerRank(
                rank.AccountId,
                Math.Max(0, rank.Mmr + delta),
                rank.Wins + (isWinner ? 1 : 0),
                rank.Losses + (isWinner ? 0 : 1),
                rank.Games + 1,
                true);

            var entity = db.PlayerRanks.SingleOrDefault(entry => entry.AccountId == rank.AccountId);
            if (entity is null)
            {
                db.PlayerRanks.Add(new PlayerRankEntity
                {
                    AccountId = rank.AccountId,
                    Mmr = next.Mmr,
                    Wins = next.Wins,
                    Losses = next.Losses,
                    Games = next.Games,
                    IsCalibrated = next.IsCalibrated,
                    UpdatedAt = now
                });
            }
            else
            {
                entity.Mmr = next.Mmr;
                entity.Wins = next.Wins;
                entity.Losses = next.Losses;
                entity.Games = next.Games;
                entity.IsCalibrated = next.IsCalibrated;
                entity.UpdatedAt = now;
            }

            _cache[rank.AccountId] = next;
        }

        db.SaveChanges();
    }

    public PlayerRank Adjust(uint accountId, int delta)
    {
        var current = GetOrCreate(accountId);
        var nextMmr = Math.Max(0, current.Mmr + delta);
        var next = current with
        {
            Mmr = nextMmr,
            // The admin adjustment is also the explicit way to assign a
            // visible rank in this server, even when no calibration matches
            // have been played yet.
            IsCalibrated = current.IsCalibrated || nextMmr > 0
        };
        Persist(next);
        return next;
    }

    public PlayerRank Reset(uint accountId)
    {
        var next = new PlayerRank(accountId, 0, 0, 0, 0, false);
        Persist(next);
        return next;
    }

    private void Persist(PlayerRank rank)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var entity = db.PlayerRanks.SingleOrDefault(entry => entry.AccountId == rank.AccountId);
        if (entity is null)
        {
            db.PlayerRanks.Add(new PlayerRankEntity
            {
                AccountId = rank.AccountId,
                Mmr = rank.Mmr,
                Wins = rank.Wins,
                Losses = rank.Losses,
                Games = rank.Games,
                IsCalibrated = rank.IsCalibrated,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            entity.Mmr = rank.Mmr;
            entity.Wins = rank.Wins;
            entity.Losses = rank.Losses;
            entity.Games = rank.Games;
            entity.IsCalibrated = rank.IsCalibrated;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        db.SaveChanges();
        _cache[rank.AccountId] = rank;
    }
}
