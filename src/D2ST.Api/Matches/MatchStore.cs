using System.Text.Json;
using D2ST.Core.Matches;
using D2ST.GameCoordinator.Matches;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Matches;

/// <summary>
/// SQLite-backed match history and profile projections. The store is a
/// singleton because GC handlers are singletons; every database operation uses
/// its own short-lived EF scope.
/// </summary>
public sealed class MatchStore : IMatchStore
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Lock _gate = new();

    public MatchStore(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public MatchRecordResult Record(MatchRecord match)
    {
        var players = match.Players
            .Where(player => player.AccountId != 0)
            .GroupBy(player => player.AccountId)
            .Select(group => group.First())
            .ToArray();

        if (match.MatchId == 0 || players.Length == 0)
        {
            return new MatchRecordResult(false);
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();

            if (db.Matches.Find(match.MatchId) is not null)
            {
                return new MatchRecordResult(false);
            }

            using var transaction = db.Database.BeginTransaction();
            if (db.Matches.Find(match.MatchId) is not null)
            {
                return new MatchRecordResult(false);
            }

            var matchEntity = new MatchEntity
            {
                MatchId = match.MatchId,
                LobbyId = match.LobbyId,
                GameMode = match.GameMode,
                DurationSeconds = match.DurationSeconds,
                EndedAt = match.EndedAt,
                GoodGuysWin = match.GoodGuysWin,
                WinningTeam = match.WinningTeam,
                FirstBloodTime = match.FirstBloodTime,
                RadiantScore = match.RadiantScore,
                DireScore = match.DireScore,
                TowerStatusJson = Serialize(match.TowerStatus),
                BarracksStatusJson = Serialize(match.BarracksStatus),
                TeamScoresJson = Serialize(match.TeamScores),
                Cluster = match.Cluster,
                ServerAddress = match.ServerAddress,
                EventScore = match.EventScore,
                AutomaticSurrender = match.AutomaticSurrender,
                ServerVersion = match.ServerVersion,
                PreGameDuration = match.PreGameDuration,
                AverageNetworthDelta = match.AverageNetworthDelta,
                MatchFlags = match.MatchFlags,
                CreatedAt = DateTimeOffset.UtcNow
            };

            foreach (var player in players)
            {
                matchEntity.Players.Add(new MatchPlayerEntity
                {
                    MatchId = match.MatchId,
                    AccountId = player.AccountId,
                    SteamId = player.SteamId,
                    Team = player.Team,
                    HeroId = player.HeroId,
                    Won = player.Won,
                    Gold = player.Gold,
                    Kills = player.Kills,
                    Deaths = player.Deaths,
                    Assists = player.Assists,
                    LeaverStatus = player.LeaverStatus,
                    LastHits = player.LastHits,
                    Denies = player.Denies,
                    GoldPerMin = player.GoldPerMin,
                    XpPerMinute = player.XpPerMinute,
                    GoldSpent = player.GoldSpent,
                    Level = player.Level,
                    ScaledHeroDamage = player.ScaledHeroDamage,
                    ScaledTowerDamage = player.ScaledTowerDamage,
                    ScaledHeroHealing = player.ScaledHeroHealing,
                    TimeLastSeen = player.TimeLastSeen,
                    SupportAbilityValue = player.SupportAbilityValue,
                    PartyId = player.PartyId,
                    ClaimedFarmGold = player.ClaimedFarmGold,
                    SupportGold = player.SupportGold,
                    ClaimedDenies = player.ClaimedDenies,
                    ClaimedMisses = player.ClaimedMisses,
                    Misses = player.Misses,
                    NetWorth = player.NetWorth,
                    HeroDamage = player.HeroDamage,
                    TowerDamage = player.TowerDamage,
                    HeroHealing = player.HeroHealing,
                    MatchPlayerFlags = player.MatchPlayerFlags,
                    HeroPickOrder = player.HeroPickOrder,
                    HeroWasRandomed = player.HeroWasRandomed,
                    Lane = player.Lane,
                    ItemsJson = Serialize(player.Items),
                    ItemPurchaseTimesJson = Serialize(player.ItemPurchaseTimes)
                });

                ApplyProfileStats(db, match, player);
                ApplyHeroStats(db, match, player);
            }

            db.Matches.Add(matchEntity);
            db.SaveChanges();
            transaction.Commit();
            return new MatchRecordResult(true);
        }
    }

    public PlayerProfileStats GetProfileStats(uint accountId)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var entity = db.PlayerProfileStats.AsNoTracking()
            .SingleOrDefault(stats => stats.AccountId == accountId);

        return entity is null
            ? PlayerProfileStats.Empty(accountId)
            : new PlayerProfileStats
            {
                AccountId = entity.AccountId,
                Games = entity.Games,
                Wins = entity.Wins,
                Losses = entity.Losses,
                TotalKills = entity.TotalKills,
                TotalDeaths = entity.TotalDeaths,
                TotalAssists = entity.TotalAssists,
                TotalLastHits = entity.TotalLastHits,
                TotalDenies = entity.TotalDenies,
                TotalHeroDamage = entity.TotalHeroDamage,
                TotalTowerDamage = entity.TotalTowerDamage,
                TotalHeroHealing = entity.TotalHeroHealing,
                TotalGoldSpent = entity.TotalGoldSpent,
                TotalGoldPerMin = entity.TotalGoldPerMin,
                TotalXpPerMinute = entity.TotalXpPerMinute,
                TotalPlayTimeSeconds = entity.TotalPlayTimeSeconds,
                LeaverCount = entity.LeaverCount,
                LastMatchAt = entity.LastMatchAt
            };
    }

    private static void ApplyProfileStats(D2stDbContext db, MatchRecord match, MatchPlayerRecord player)
    {
        var entity = db.PlayerProfileStats
            .SingleOrDefault(stats => stats.AccountId == player.AccountId);
        if (entity is null)
        {
            entity = new PlayerProfileStatsEntity { AccountId = player.AccountId };
            db.PlayerProfileStats.Add(entity);
        }

        entity.Games++;
        if (player.Won)
        {
            entity.Wins++;
        }
        else
        {
            entity.Losses++;
        }

        entity.TotalKills += player.Kills;
        entity.TotalDeaths += player.Deaths;
        entity.TotalAssists += player.Assists;
        entity.TotalLastHits += player.LastHits;
        entity.TotalDenies += player.Denies;
        entity.TotalHeroDamage += player.HeroDamage != 0 ? player.HeroDamage : player.ScaledHeroDamage;
        entity.TotalTowerDamage += player.TowerDamage != 0 ? player.TowerDamage : player.ScaledTowerDamage;
        entity.TotalHeroHealing += player.HeroHealing != 0 ? player.HeroHealing : player.ScaledHeroHealing;
        entity.TotalGoldSpent += player.GoldSpent;
        entity.TotalGoldPerMin += player.GoldPerMin;
        entity.TotalXpPerMinute += player.XpPerMinute;
        entity.TotalPlayTimeSeconds += match.DurationSeconds;
        if (player.LeaverStatus != 0)
        {
            entity.LeaverCount++;
        }

        if (entity.LastMatchAt is null || entity.LastMatchAt < match.EndedAt)
        {
            entity.LastMatchAt = match.EndedAt;
        }
    }

    private static void ApplyHeroStats(D2stDbContext db, MatchRecord match, MatchPlayerRecord player)
    {
        if (player.HeroId <= 0)
        {
            return;
        }

        var entity = db.PlayerHeroStats.SingleOrDefault(stats =>
            stats.AccountId == player.AccountId && stats.HeroId == player.HeroId);
        if (entity is null)
        {
            entity = new PlayerHeroStatsEntity
            {
                AccountId = player.AccountId,
                HeroId = player.HeroId
            };
            db.PlayerHeroStats.Add(entity);
        }

        entity.Games++;
        if (player.Won)
        {
            entity.Wins++;
        }
        else
        {
            entity.Losses++;
        }

        entity.TotalKills += player.Kills;
        entity.TotalDeaths += player.Deaths;
        entity.TotalAssists += player.Assists;
        entity.TotalLastHits += player.LastHits;
        entity.TotalDenies += player.Denies;
        entity.TotalHeroDamage += player.HeroDamage != 0 ? player.HeroDamage : player.ScaledHeroDamage;
        entity.TotalTowerDamage += player.TowerDamage != 0 ? player.TowerDamage : player.ScaledTowerDamage;
        entity.TotalHeroHealing += player.HeroHealing != 0 ? player.HeroHealing : player.ScaledHeroHealing;
        entity.TotalGoldSpent += player.GoldSpent;
        entity.TotalGoldPerMin += player.GoldPerMin;
        entity.TotalXpPerMinute += player.XpPerMinute;
        if (entity.LastMatchAt is null || entity.LastMatchAt < match.EndedAt)
        {
            entity.LastMatchAt = match.EndedAt;
        }
    }

    private static string Serialize<T>(IReadOnlyList<T> values) =>
        JsonSerializer.Serialize(values);
}
