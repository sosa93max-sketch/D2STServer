using System.Text.Json;
using D2ST.Core.Economy;
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
                ApplyWinReward(db, match.MatchId, player);
            }

            db.Matches.Add(matchEntity);
            db.SaveChanges();
            transaction.Commit();
            return new MatchRecordResult(true);
        }
    }

    public IReadOnlyList<PlayerMatchHistoryEntry> GetPlayerMatchHistory(
        uint accountId,
        ulong startAtMatchId,
        uint matchesRequested,
        int heroId,
        bool includePracticeMatches,
        bool includeCustomGames,
        bool includeEventGames)
    {
        // Every row currently written by D2STServer comes from a practice
        // lobby. Respect an explicit request to exclude that category while
        // leaving the other filters ready for future match sources.
        if (accountId == 0 || !includePracticeMatches)
        {
            return [];
        }

        var limit = (int)Math.Min(matchesRequested == 0 ? 20u : matchesRequested, 100u);
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var rows = (
            from player in db.MatchPlayers.AsNoTracking()
            join match in db.Matches.AsNoTracking() on player.MatchId equals match.MatchId
            where player.AccountId == accountId
                && (startAtMatchId == 0 || match.MatchId < startAtMatchId)
                && (heroId <= 0 || player.HeroId == heroId)
            orderby match.MatchId descending
            select new
            {
                match.MatchId,
                match.EndedAt,
                match.DurationSeconds,
                match.GameMode,
                player.HeroId,
                player.Won,
                player.LeaverStatus
            })
            .Take(limit)
            .ToList();

        return rows.Select(row => new PlayerMatchHistoryEntry
        {
            MatchId = row.MatchId,
            StartTime = UnixStartTime(row.EndedAt, row.DurationSeconds),
            HeroId = row.HeroId,
            Winner = row.Won,
            GameMode = row.GameMode,
            DurationSeconds = row.DurationSeconds,
            Abandon = row.LeaverStatus != 0
        }).ToArray();
    }

    public IReadOnlyList<MatchMinimalRecord> GetMatchesMinimal(IReadOnlyList<ulong> matchIds)
    {
        var ids = matchIds
            .Where(matchId => matchId != 0)
            .Distinct()
            .Take(100)
            .ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var matches = db.Matches.AsNoTracking()
            .Where(match => ids.Contains(match.MatchId))
            .OrderByDescending(match => match.MatchId)
            .ToList();
        var players = db.MatchPlayers.AsNoTracking()
            .Where(player => ids.Contains(player.MatchId))
            .OrderBy(player => player.MatchId)
            .ThenBy(player => player.AccountId)
            .ToList();
        var playersByMatch = players
            .GroupBy(player => player.MatchId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return matches.Select(match => new MatchMinimalRecord
        {
            MatchId = match.MatchId,
            StartTime = UnixStartTime(match.EndedAt, match.DurationSeconds),
            DurationSeconds = match.DurationSeconds,
            GameMode = match.GameMode,
            WinningTeam = match.WinningTeam,
            RadiantScore = match.RadiantScore,
            DireScore = match.DireScore,
            Players = playersByMatch.TryGetValue(match.MatchId, out var matchPlayers)
                ? matchPlayers.Select(ToMinimalPlayer).ToArray()
                : Array.Empty<MatchMinimalPlayer>()
        }).ToArray();
    }

    public IReadOnlyList<TeammateStatRecord> GetTeammateStats(uint accountId)
    {
        if (accountId == 0)
        {
            return [];
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var ownTeams = db.MatchPlayers.AsNoTracking()
            .Where(player => player.AccountId == accountId)
            .Select(player => new { player.MatchId, player.Team })
            .ToList()
            .GroupBy(row => row.MatchId)
            .ToDictionary(group => group.Key, group => group.First().Team);
        if (ownTeams.Count == 0)
        {
            return [];
        }

        var matchIds = ownTeams.Keys.ToArray();
        var candidates = (
            from player in db.MatchPlayers.AsNoTracking()
            join match in db.Matches.AsNoTracking() on player.MatchId equals match.MatchId
            where player.AccountId != accountId && matchIds.Contains(player.MatchId)
            select new { Player = player, match.EndedAt }
        ).ToList();

        var sharedGames = candidates
            .Where(candidate => ownTeams.TryGetValue(candidate.Player.MatchId, out var ownTeam)
                && ownTeam == candidate.Player.Team)
            .ToList();

        return sharedGames
            .GroupBy(candidate => candidate.Player.AccountId)
            .Select(group =>
            {
                var latest = group.OrderByDescending(entry => entry.EndedAt).First();
                return new TeammateStatRecord
                {
                    AccountId = group.Key,
                    Games = (uint)group.Count(),
                    Wins = (uint)group.Count(entry => entry.Player.Won),
                    MostRecentGameTimestamp = UnixTimestamp(latest.EndedAt),
                    MostRecentGameMatchId = latest.Player.MatchId,
                    Performance = (float)group.Average(entry =>
                        (double)entry.Player.Kills + entry.Player.Assists - entry.Player.Deaths)
                };
            })
            .OrderByDescending(stat => stat.Games)
            .ThenByDescending(stat => stat.MostRecentGameTimestamp)
            .Take(100)
            .ToArray();
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

    public IReadOnlyList<HeroStatsRecord> GetHeroStandings(uint accountId)
    {
        if (accountId == 0)
        {
            return [];
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var rows = db.PlayerHeroStats.AsNoTracking()
            .Where(stats => stats.AccountId == accountId && stats.HeroId > 0)
            .ToList();

        return rows
            .Select(ToHeroStats)
            .OrderByDescending(stats => stats.Wins)
            .ThenByDescending(stats => stats.Games)
            .ThenBy(stats => stats.HeroId)
            .ToArray();
    }

    public IReadOnlyList<HeroStatsRecord> GetHeroStats(uint accountId)
    {
        if (accountId == 0)
        {
            return [];
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        return db.PlayerHeroStats.AsNoTracking()
            .Where(stats => stats.AccountId == accountId && stats.HeroId > 0)
            .ToList()
            .Select(ToHeroStats)
            .OrderBy(stats => stats.Games)
            .ThenBy(stats => stats.HeroId)
            .ToArray();
    }

    public IReadOnlyList<int> GetHeroOrder()
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        return db.PlayerHeroStats.AsNoTracking()
            .Where(stats => stats.HeroId > 0)
            .Select(stats => stats.HeroId)
            .Distinct()
            .OrderBy(heroId => heroId)
            .ToArray();
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

    private static void ApplyWinReward(D2stDbContext db, ulong matchId, MatchPlayerRecord player)
    {
        // A leaver can still appear on the winning side in a malformed or
        // partial sign-out. Do not award that row; the reward is for a clean
        // human victory and is deliberately tied to the same 7004 transaction.
        if (!player.Won || player.LeaverStatus != 0)
        {
            return;
        }

        var reference = $"match-win:{matchId}:{player.AccountId}";
        if (db.WalletTransactions.Any(transaction => transaction.Reference == reference))
        {
            return;
        }

        var wallet = db.Wallets.SingleOrDefault(row => row.AccountId == player.AccountId);
        if (wallet is null)
        {
            wallet = new WalletEntity { AccountId = player.AccountId };
            db.Wallets.Add(wallet);
        }

        wallet.BalanceCredits = checked(wallet.BalanceCredits + EconomyRules.MatchWinRewardCredits);
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        db.WalletTransactions.Add(new WalletTransactionEntity
        {
            AccountId = player.AccountId,
            Kind = EconomyTransactionKind.MatchWinReward,
            AmountCredits = EconomyRules.MatchWinRewardCredits,
            BalanceAfterCredits = wallet.BalanceCredits,
            Reference = reference,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static MatchMinimalPlayer ToMinimalPlayer(MatchPlayerEntity player) => new()
    {
        AccountId = player.AccountId,
        HeroId = player.HeroId,
        Level = player.Level,
        Kills = player.Kills,
        Deaths = player.Deaths,
        Assists = player.Assists,
        PlayerSlot = player.Team == 1 ? 128u : 0u,
        Items = DeserializeItems(player.ItemsJson)
    };

    private static HeroStatsRecord ToHeroStats(PlayerHeroStatsEntity stats) => new()
    {
        HeroId = stats.HeroId,
        Games = stats.Games,
        Wins = stats.Wins,
        Losses = stats.Losses,
        TotalKills = stats.TotalKills,
        TotalDeaths = stats.TotalDeaths,
        TotalAssists = stats.TotalAssists,
        TotalLastHits = stats.TotalLastHits,
        TotalDenies = stats.TotalDenies,
        TotalHeroDamage = stats.TotalHeroDamage,
        TotalTowerDamage = stats.TotalTowerDamage,
        TotalHeroHealing = stats.TotalHeroHealing,
        TotalGoldSpent = stats.TotalGoldSpent,
        TotalGoldPerMin = stats.TotalGoldPerMin,
        TotalXpPerMinute = stats.TotalXpPerMinute,
        LastMatchAt = stats.LastMatchAt
    };

    private static HeroStatsRecord ToHeroStats(
        IGrouping<int, PlayerHeroStatsEntity> group)
    {
        return new HeroStatsRecord
        {
            HeroId = group.Key,
            Games = group.Sum(stats => stats.Games),
            Wins = group.Sum(stats => stats.Wins),
            Losses = group.Sum(stats => stats.Losses),
            TotalKills = group.Sum(stats => stats.TotalKills),
            TotalDeaths = group.Sum(stats => stats.TotalDeaths),
            TotalAssists = group.Sum(stats => stats.TotalAssists),
            TotalLastHits = group.Sum(stats => stats.TotalLastHits),
            TotalDenies = group.Sum(stats => stats.TotalDenies),
            TotalHeroDamage = group.Sum(stats => stats.TotalHeroDamage),
            TotalTowerDamage = group.Sum(stats => stats.TotalTowerDamage),
            TotalHeroHealing = group.Sum(stats => stats.TotalHeroHealing),
            TotalGoldSpent = group.Sum(stats => stats.TotalGoldSpent),
            TotalGoldPerMin = group.Sum(stats => stats.TotalGoldPerMin),
            TotalXpPerMinute = group.Sum(stats => stats.TotalXpPerMinute),
            LastMatchAt = group.Max(stats => stats.LastMatchAt)
        };
    }

    private static IReadOnlyList<int> DeserializeItems(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();
        }
        catch (JsonException)
        {
            return Array.Empty<int>();
        }
    }

    private static uint UnixStartTime(DateTimeOffset endedAt, uint durationSeconds)
    {
        var start = endedAt.ToUnixTimeSeconds() - durationSeconds;
        return start <= 0 ? 0u : start >= uint.MaxValue ? uint.MaxValue : (uint)start;
    }

    private static uint UnixTimestamp(DateTimeOffset value)
    {
        var timestamp = value.ToUnixTimeSeconds();
        return timestamp <= 0 ? 0u : timestamp >= uint.MaxValue ? uint.MaxValue : (uint)timestamp;
    }

    private static string Serialize<T>(IReadOnlyList<T> values) =>
        JsonSerializer.Serialize(values);
}
