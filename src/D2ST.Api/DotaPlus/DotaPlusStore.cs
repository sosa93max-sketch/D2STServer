using D2ST.GameCoordinator.DotaPlus;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.DotaPlus;

/// <summary>
/// SQLite-backed local Dota Plus store. Subscription, shards, challenges and
/// relic purchases belong to the LAN server; no billing or Valve service is
/// involved.
/// </summary>
public sealed class DotaPlusStore : IDotaPlusStore
{
    private const uint PlusEventId = 19;
    private const uint FirstSequenceId = 1001;
    private const long CommonRelicCost = 800;
    private const long RareRelicCost = 1600;

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    public DotaPlusStore(IServiceScopeFactory scopes, TimeProvider time)
    {
        _scopes = scopes;
        _time = time;
    }

    public DotaPlusState Get(uint accountId)
    {
        if (accountId == 0)
        {
            return DotaPlusState.Inactive(accountId);
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var entity = db.DotaPlusAccounts.AsNoTracking()
            .SingleOrDefault(account => account.AccountId == accountId);
        return entity is null ? DotaPlusState.Inactive(accountId) : ToState(entity);
    }

    public IReadOnlyDictionary<uint, DotaPlusState> GetMany(IReadOnlyCollection<uint> accountIds)
    {
        var ids = accountIds.Where(id => id != 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<uint, DotaPlusState>();
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var rows = db.DotaPlusAccounts.AsNoTracking()
            .Where(account => ids.Contains(account.AccountId))
            .ToList()
            .ToDictionary(account => account.AccountId, ToState);

        foreach (var accountId in ids)
        {
            rows.TryAdd(accountId, DotaPlusState.Inactive(accountId));
        }

        return rows;
    }

    public DotaPlusMutationResult UpdateSubscription(
        uint accountId,
        bool enabled,
        int days,
        bool extend,
        uint changedByAccountId,
        string? reason)
    {
        if (accountId == 0)
        {
            return Failed(accountId, "invalid_account", "La cuenta no es válida.");
        }

        if (enabled && (days < 1 || days > 3650))
        {
            return Failed(accountId, "invalid_days", "Los días deben estar entre 1 y 3650.");
        }

        if (!enabled && days != 0)
        {
            return Failed(accountId, "invalid_days", "La revocación no acepta días.");
        }

        var normalizedReason = NormalizeReason(
            reason,
            enabled ? "Activación administrativa" : "Revocación administrativa");
        if (normalizedReason is null)
        {
            return Failed(accountId, "invalid_reason", "El motivo no puede superar 256 caracteres.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            if (!db.Accounts.Any(account => account.AccountId == accountId))
            {
                return Failed(accountId, "account_not_found", "Usuario no encontrado.");
            }

            using var transaction = db.Database.BeginTransaction();
            var now = _time.GetUtcNow();
            var entity = db.DotaPlusAccounts.SingleOrDefault(account => account.AccountId == accountId);
            var wasActive = entity is not null && ToState(entity).IsActiveAt(now);
            if (entity is null)
            {
                entity = new DotaPlusAccountEntity { AccountId = accountId };
                db.DotaPlusAccounts.Add(entity);
            }

            if (enabled)
            {
                var baseDate = extend && wasActive && entity.ExpiresAt is not null
                    ? entity.ExpiresAt.Value
                    : now;
                entity.Enabled = true;
                entity.StartedAt ??= now;
                entity.ExpiresAt = baseDate.AddDays(days);
            }
            else
            {
                entity.Enabled = false;
                entity.ExpiresAt = now;
            }

            entity.UpdatedAt = now;
            var action = enabled
                ? (wasActive && extend ? "extend" : "activate")
                : "revoke";
            db.DotaPlusTransactions.Add(new DotaPlusTransactionEntity
            {
                AccountId = accountId,
                ChangedByAccountId = changedByAccountId,
                Action = action,
                Days = enabled ? days : 0,
                Reason = normalizedReason,
                ExpiresAtAfter = entity.ExpiresAt,
                CreatedAt = now
            });

            db.SaveChanges();
            transaction.Commit();

            var state = ToState(entity);
            var message = enabled
                ? wasActive && extend
                    ? $"Dota Plus ampliado {days} días."
                    : $"Dota Plus activado por {days} días."
                : "Dota Plus revocado.";
            return new DotaPlusMutationResult(true, "ok", message, state);
        }
    }

    public DotaPlusSnapshot GetSnapshot(uint accountId)
    {
        if (accountId == 0)
        {
            return EmptySnapshot(accountId);
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        return Snapshot(db, accountId, _time.GetUtcNow());
    }

    public IReadOnlyDictionary<uint, DotaPlusSnapshot> GetManySnapshots(
        IReadOnlyCollection<uint> accountIds)
    {
        var ids = accountIds.Where(id => id != 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<uint, DotaPlusSnapshot>();
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var now = _time.GetUtcNow();
        var accounts = db.DotaPlusAccounts.AsNoTracking()
            .Where(row => ids.Contains(row.AccountId))
            .ToDictionary(row => row.AccountId);
        var challenges = db.DotaPlusChallenges.AsNoTracking()
            .Where(row => ids.Contains(row.AccountId))
            .ToList()
            .GroupBy(row => row.AccountId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DotaPlusChallenge>)group
                    .OrderBy(row => row.SlotId)
                    .Select(ToChallenge)
                    .ToArray());

        return ids.ToDictionary(
            accountId => accountId,
            accountId =>
            {
                accounts.TryGetValue(accountId, out var account);
                var active = account is not null && ToState(account).IsActiveAt(now);
                return new DotaPlusSnapshot(
                    accountId,
                    active,
                    Math.Max(0, account?.Shards ?? 0),
                    challenges.GetValueOrDefault(accountId) ?? []);
            });
    }

    public DotaPlusSnapshot EnsureChallenges(uint accountId)
    {
        if (accountId == 0)
        {
            return EmptySnapshot(accountId);
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var now = _time.GetUtcNow();
            var account = db.DotaPlusAccounts.SingleOrDefault(row => row.AccountId == accountId);
            if (account is null || !ToState(account).IsActiveAt(now))
            {
                return Snapshot(db, accountId, now);
            }

            EnsureDefaultChallenges(db, accountId, now);
            db.SaveChanges();
            return Snapshot(db, accountId, now);
        }
    }

    public DotaPlusProgressResult ApplyMatchProgress(
        uint accountId,
        ulong matchId,
        int heroId,
        bool won,
        uint kills,
        uint durationSeconds)
    {
        if (accountId == 0 || matchId == 0)
        {
            return FailedProgress(accountId, "invalid_match", "La partida local no es válida.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var now = _time.GetUtcNow();
            var account = db.DotaPlusAccounts.SingleOrDefault(row => row.AccountId == accountId);
            if (account is null || !ToState(account).IsActiveAt(now))
            {
                return FailedProgress(accountId, "subscription_required", "Dota Plus no está activo.");
            }

            EnsureDefaultChallenges(db, accountId, now);
            var reference = $"match:{matchId}:account:{accountId}";
            var reportReference = $"challenge-report:{matchId}:account:{accountId}";
            if (db.DotaPlusShardTransactions.Any(row =>
                    row.Reference == reference || row.Reference == reportReference))
            {
                return new DotaPlusProgressResult(
                    true,
                    "already_applied",
                    "La recompensa de esta partida ya fue aplicada.",
                    Snapshot(db, accountId, now),
                    0);
            }

            var earned = 100L + (won ? 50L : 0L) + (durationSeconds >= 1200 ? 50L : 0L);
            foreach (var challenge in db.DotaPlusChallenges
                         .Where(row => row.AccountId == accountId)
                         .OrderBy(row => row.SlotId)
                         .ToList())
            {
                var before = challenge.Completed;
                var increment = challenge.SlotId switch
                {
                    1 => 1u,
                    2 => won ? 1u : 0u,
                    3 => kills,
                    _ => 0u
                };
                challenge.Completed = Math.Min(challenge.IntParam0, challenge.Completed + increment);
                challenge.UpdatedAt = now;
                challenge.LastMatchReference = reference;
                if (before < challenge.IntParam0 && challenge.Completed >= challenge.IntParam0)
                {
                    earned += challenge.IntParam1;
                    challenge.Attempts++;
                }
            }

            account.Shards += earned;
            account.UpdatedAt = now;
            AddShardTransaction(
                db,
                account,
                earned,
                0,
                reference,
                "Recompensa de partida local",
                now);
            db.SaveChanges();

            return new DotaPlusProgressResult(
                true,
                "ok",
                $"Partida procesada: +{earned} shards.",
                Snapshot(db, accountId, now),
                earned);
        }
    }

    public DotaPlusProgressResult ApplyChallengeReport(
        uint accountId,
        ulong matchId,
        int heroId,
        IReadOnlyList<DotaPlusChallengeReport> reports)
    {
        if (accountId == 0 || matchId == 0)
        {
            return FailedProgress(accountId, "invalid_match", "La partida local no es válida.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var now = _time.GetUtcNow();
            var account = db.DotaPlusAccounts.SingleOrDefault(row => row.AccountId == accountId);
            if (account is null || !ToState(account).IsActiveAt(now))
            {
                return FailedProgress(accountId, "subscription_required", "Dota Plus no está activo.");
            }

            EnsureDefaultChallenges(db, accountId, now);
            var reference = $"challenge-report:{matchId}:account:{accountId}";
            var matchReference = $"match:{matchId}:account:{accountId}";
            if (db.DotaPlusShardTransactions.Any(row =>
                    row.Reference == reference || row.Reference == matchReference))
            {
                return new DotaPlusProgressResult(
                    true,
                    "already_applied",
                    "El progreso de esta partida ya fue aplicado.",
                    Snapshot(db, accountId, now),
                    0);
            }

            var earned = 0L;
            foreach (var report in reports)
            {
                var challenge = db.DotaPlusChallenges.SingleOrDefault(row =>
                    row.AccountId == accountId
                    && row.SlotId == report.SlotId
                    && row.SequenceId == report.SequenceId);
                if (challenge is null)
                {
                    continue;
                }

                var before = challenge.Completed;
                challenge.Completed = Math.Min(
                    challenge.IntParam0,
                    Math.Max(challenge.Completed, report.Progress));
                if (report.ChallengeRank != 0)
                {
                    challenge.QuestRank = Math.Min(challenge.MaxQuestRank, report.ChallengeRank);
                }

                challenge.UpdatedAt = now;
                challenge.LastMatchReference = reference;
                if (before < challenge.IntParam0 && challenge.Completed >= challenge.IntParam0)
                {
                    earned += challenge.IntParam1;
                    challenge.Attempts++;
                }
            }

            account.Shards += earned;
            account.UpdatedAt = now;
            AddShardTransaction(
                db,
                account,
                earned,
                0,
                reference,
                "Progreso de desafío reportado por partida",
                now);
            db.SaveChanges();

            return new DotaPlusProgressResult(
                true,
                "ok",
                earned == 0 ? "Progreso de desafíos actualizado." : $"Desafíos completados: +{earned} shards.",
                Snapshot(db, accountId, now),
                earned);
        }
    }

    public DotaPlusRerollResult RerollChallenge(
        uint accountId,
        uint sequenceId,
        int heroId)
    {
        if (accountId == 0 || sequenceId == 0)
        {
            return FailedReroll(accountId, "invalid_challenge", "El desafío no es válido.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var now = _time.GetUtcNow();
            var account = db.DotaPlusAccounts.SingleOrDefault(row => row.AccountId == accountId);
            if (account is null || !ToState(account).IsActiveAt(now))
            {
                return FailedReroll(accountId, "subscription_required", "Dota Plus no está activo.");
            }

            var challenge = db.DotaPlusChallenges.SingleOrDefault(row =>
                row.AccountId == accountId && row.SequenceId == sequenceId);
            if (challenge is null || heroId > 0 && challenge.HeroId != 0 && challenge.HeroId != heroId)
            {
                return FailedReroll(accountId, "challenge_not_found", "Desafío no encontrado.");
            }

            var nextSequence = db.DotaPlusChallenges
                .Where(row => row.AccountId == accountId)
                .Select(row => row.SequenceId)
                .DefaultIfEmpty(FirstSequenceId - 1)
                .Max() + 1;
            var definition = Definition(challenge.SlotId, nextSequence);
            challenge.IntParam0 = definition.Target;
            challenge.IntParam1 = definition.Reward;
            challenge.Completed = 0;
            challenge.SequenceId = nextSequence;
            challenge.TemplateId = definition.TemplateId;
            challenge.HeroId = definition.HeroId;
            challenge.ChallengeTier = 1;
            challenge.QuestRank = 1;
            challenge.MaxQuestRank = 3;
            challenge.Attempts++;
            challenge.CreatedAt = now;
            challenge.UpdatedAt = now;
            challenge.LastMatchReference = string.Empty;
            db.SaveChanges();

            return new DotaPlusRerollResult(
                true,
                "ok",
                "Desafío renovado.",
                Snapshot(db, accountId, now));
        }
    }

    public DotaPlusRelicResult PurchaseRelic(
        uint accountId,
        int heroId,
        int rarity)
    {
        if (accountId == 0 || heroId <= 0 || heroId > 500)
        {
            return FailedRelic(accountId, "invalid_relic", "El héroe no es válido.");
        }

        var cost = rarity switch
        {
            0 => CommonRelicCost,
            1 => RareRelicCost,
            _ => 0
        };
        if (cost == 0)
        {
            return FailedRelic(accountId, "invalid_rarity", "La rareza de la reliquia no es válida.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var now = _time.GetUtcNow();
            var account = db.DotaPlusAccounts.SingleOrDefault(row => row.AccountId == accountId);
            if (account is null || !ToState(account).IsActiveAt(now))
            {
                return FailedRelic(accountId, "subscription_required", "Dota Plus no está activo.");
            }

            if (account.Shards < cost)
            {
                return FailedRelic(accountId, "not_enough_shards", "No hay suficientes shards.");
            }

            var ownedCount = db.DotaPlusRelics.Count(row =>
                row.AccountId == accountId && row.HeroId == heroId && row.RelicRarity == rarity);
            var killEaterType = LocalKillEaterType(heroId, rarity, ownedCount + 1);
            var reference = $"relic:{accountId}:{Guid.NewGuid():N}";
            account.Shards -= cost;
            account.UpdatedAt = now;
            db.DotaPlusRelics.Add(new DotaPlusRelicEntity
            {
                AccountId = accountId,
                HeroId = heroId,
                RelicRarity = rarity,
                KillEaterType = killEaterType,
                CreatedAt = now
            });
            AddShardTransaction(
                db,
                account,
                -cost,
                0,
                reference,
                rarity == 0 ? "Compra de reliquia común" : "Compra de reliquia rara",
                now);
            db.SaveChanges();

            return new DotaPlusRelicResult(
                true,
                "ok",
                "Reliquia comprada.",
                Snapshot(db, accountId, now),
                killEaterType);
        }
    }

    public DotaPlusShardMutationResult AdjustShards(
        uint accountId,
        long delta,
        uint changedByAccountId,
        string? reason)
    {
        if (accountId == 0 || delta == 0 || delta == long.MinValue)
        {
            return FailedShards(accountId, "invalid_delta", "El ajuste de shards no es válido.");
        }

        var normalizedReason = NormalizeReason(reason, "Ajuste administrativo de shards");
        if (normalizedReason is null)
        {
            return FailedShards(accountId, "invalid_reason", "El motivo no puede superar 256 caracteres.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            if (!db.Accounts.Any(row => row.AccountId == accountId))
            {
                return FailedShards(accountId, "account_not_found", "Usuario no encontrado.");
            }

            var now = _time.GetUtcNow();
            var account = db.DotaPlusAccounts.SingleOrDefault(row => row.AccountId == accountId);
            if (account is null)
            {
                account = new DotaPlusAccountEntity
                {
                    AccountId = accountId,
                    UpdatedAt = now
                };
                db.DotaPlusAccounts.Add(account);
            }

            if (delta < 0 && account.Shards < -delta)
            {
                return FailedShards(accountId, "not_enough_shards", "El saldo de shards no puede quedar negativo.");
            }

            if (delta > 0 && account.Shards > long.MaxValue - delta)
            {
                return FailedShards(accountId, "shards_overflow", "El saldo de shards supera el máximo permitido.");
            }

            account.Shards += delta;
            account.UpdatedAt = now;
            AddShardTransaction(
                db,
                account,
                delta,
                changedByAccountId,
                $"admin:{accountId}:{Guid.NewGuid():N}",
                normalizedReason,
                now);
            db.SaveChanges();

            return new DotaPlusShardMutationResult(
                true,
                "ok",
                delta > 0 ? $"Se añadieron {delta} shards." : $"Se retiraron {-delta} shards.",
                Snapshot(db, accountId, now));
        }
    }

    private static void EnsureDefaultChallenges(D2stDbContext db, uint accountId, DateTimeOffset now)
    {
        var existing = db.DotaPlusChallenges
            .Where(row => row.AccountId == accountId)
            .ToDictionary(row => row.SlotId);
        foreach (var slotId in new uint[] { 1, 2, 3 })
        {
            if (existing.ContainsKey(slotId))
            {
                continue;
            }

            var sequenceId = FirstSequenceId + slotId - 1;
            var definition = Definition(slotId, sequenceId);
            db.DotaPlusChallenges.Add(new DotaPlusChallengeEntity
            {
                AccountId = accountId,
                SlotId = slotId,
                EventId = PlusEventId,
                IntParam0 = definition.Target,
                IntParam1 = definition.Reward,
                CreatedAt = now,
                Completed = 0,
                SequenceId = sequenceId,
                ChallengeTier = 1,
                Flags = 0,
                Attempts = 0,
                CompleteLimit = 1,
                QuestRank = 1,
                MaxQuestRank = 3,
                InstanceId = sequenceId,
                HeroId = definition.HeroId,
                TemplateId = definition.TemplateId,
                LastMatchReference = string.Empty,
                UpdatedAt = now
            });
        }
    }

    private static (uint Target, uint Reward, int HeroId, uint TemplateId) Definition(
        uint slotId,
        uint sequenceId) => slotId switch
        {
            1 => (1 + sequenceId % 2, 100, 0, 1001 + sequenceId % 2),
            2 => (1, 250, 0, 2001 + sequenceId % 3),
            3 => (10 + sequenceId % 6, 500, 0, 3001 + sequenceId % 4),
            _ => (1, 100, 0, 9000 + slotId)
        };

    private static void AddShardTransaction(
        D2stDbContext db,
        DotaPlusAccountEntity account,
        long amount,
        uint changedByAccountId,
        string reference,
        string reason,
        DateTimeOffset now)
    {
        db.DotaPlusShardTransactions.Add(new DotaPlusShardTransactionEntity
        {
            AccountId = account.AccountId,
            ChangedByAccountId = changedByAccountId,
            Amount = amount,
            BalanceAfter = account.Shards,
            Reference = reference,
            Reason = reason,
            CreatedAt = now
        });
    }

    private static DotaPlusSnapshot Snapshot(
        D2stDbContext db,
        uint accountId,
        DateTimeOffset now)
    {
        var account = db.DotaPlusAccounts.AsNoTracking()
            .SingleOrDefault(row => row.AccountId == accountId);
        var challenges = db.DotaPlusChallenges.AsNoTracking()
            .Where(row => row.AccountId == accountId)
            .OrderBy(row => row.SlotId)
            .ToList()
            .Select(ToChallenge)
            .ToArray();
        var active = account is not null && ToState(account).IsActiveAt(now);
        return new DotaPlusSnapshot(accountId, active, Math.Max(0, account?.Shards ?? 0), challenges);
    }

    private static DotaPlusChallenge ToChallenge(DotaPlusChallengeEntity row) =>
        new(
            row.AccountId,
            row.EventId,
            row.SlotId,
            row.IntParam0,
            row.IntParam1,
            row.CreatedAt,
            row.Completed,
            row.SequenceId,
            row.ChallengeTier,
            row.Flags,
            row.Attempts,
            row.CompleteLimit,
            row.QuestRank,
            row.MaxQuestRank,
            row.InstanceId,
            row.HeroId,
            row.TemplateId);

    private DotaPlusMutationResult Failed(uint accountId, string code, string message) =>
        new(false, code, message, Get(accountId));

    private DotaPlusProgressResult FailedProgress(uint accountId, string code, string message) =>
        new(false, code, message, GetSnapshot(accountId), 0);

    private DotaPlusRerollResult FailedReroll(uint accountId, string code, string message) =>
        new(false, code, message, GetSnapshot(accountId));

    private DotaPlusRelicResult FailedRelic(uint accountId, string code, string message) =>
        new(false, code, message, GetSnapshot(accountId), 0);

    private DotaPlusShardMutationResult FailedShards(uint accountId, string code, string message) =>
        new(false, code, message, GetSnapshot(accountId));

    private static DotaPlusSnapshot EmptySnapshot(uint accountId) =>
        new(accountId, false, 0, []);

    private static DotaPlusState ToState(DotaPlusAccountEntity entity) =>
        new(
            entity.AccountId,
            entity.Enabled,
            entity.StartedAt,
            entity.ExpiresAt,
            entity.PlusFlags,
            entity.SteamAgreementId);

    private static string? NormalizeReason(string? reason, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
        return value.Length > 256 ? null : value;
    }

    private static uint LocalKillEaterType(int heroId, int rarity, int ownedCount) =>
        0xD2000000u
        | ((uint)heroId & 0x3FFu) << 10
        | ((uint)rarity & 0x3u) << 8
        | ((uint)ownedCount & 0xFFu);
}
