using D2ST.GameCoordinator.DotaPlus;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.DotaPlus;

/// <summary>
/// SQLite-backed local Dota Plus subscription store. Billing is intentionally
/// outside this class: an administrator or a later local-catalog purchase is
/// the authority that changes the entitlement.
/// </summary>
public sealed class DotaPlusStore : IDotaPlusStore
{
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

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? (enabled ? "Activación administrativa" : "Revocación administrativa")
            : reason.Trim();
        if (normalizedReason.Length > 256)
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

    private DotaPlusState ToState(DotaPlusAccountEntity entity) =>
        new(
            entity.AccountId,
            entity.Enabled,
            entity.StartedAt,
            entity.ExpiresAt,
            entity.PlusFlags,
            entity.SteamAgreementId);

    private DotaPlusMutationResult Failed(uint accountId, string code, string message) =>
        new(false, code, message, Get(accountId));
}
