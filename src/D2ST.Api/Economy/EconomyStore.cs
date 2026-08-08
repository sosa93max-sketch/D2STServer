using System.Text.Json;
using D2ST.Core.Economy;
using D2ST.GameCoordinator.Econ;
using D2ST.Persistence;
using D2ST.Protocol.Dota;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Economy;

/// <summary>
/// SQLite-backed local economy. Wallet mutations, purchase transactions and
/// durable econ items are kept here; the GC layer only projects the resulting
/// items into Shared Object messages.
/// </summary>
public sealed class EconomyStore : IEconomyStore
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Lock _gate = new();

    public EconomyStore(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public IReadOnlyList<StoreCatalogItem> GetCatalog(bool activeOnly = true)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var products = db.StoreCatalogItems.AsNoTracking()
            .Where(item => !activeOnly || item.Active)
            .OrderBy(item => item.ProductType)
            .ThenBy(item => item.Name)
            .ToList();
        var components = db.StoreCatalogComponents.AsNoTracking().ToList();
        return products.Select(item => ToCatalog(item, components)).ToArray();
    }

    public StoreCatalogItem? FindProduct(uint productIdOrDefIndex, bool activeOnly = true)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var item = db.StoreCatalogItems.AsNoTracking()
            .Where(candidate => (!activeOnly || candidate.Active)
                && (candidate.ProductId == productIdOrDefIndex || candidate.DefIndex == productIdOrDefIndex))
            .OrderBy(candidate => candidate.ProductId == productIdOrDefIndex ? 0 : 1)
            .FirstOrDefault();
        if (item is null)
        {
            return null;
        }

        var components = db.StoreCatalogComponents.AsNoTracking()
            .Where(component => component.ProductId == item.ProductId)
            .ToList();
        return ToCatalog(item, components);
    }

    public WalletSnapshot GetWallet(uint accountId)
    {
        if (accountId == 0)
        {
            return WalletSnapshot.Empty(accountId);
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var wallet = db.Wallets.AsNoTracking().SingleOrDefault(row => row.AccountId == accountId);
        return wallet is null ? WalletSnapshot.Empty(accountId) : ToWallet(wallet);
    }

    public WalletAdjustmentResult AdjustWallet(uint accountId, long delta, string reference)
    {
        if (accountId == 0)
        {
            return WalletAdjustmentResult.Failed("invalid_account", "La cuenta no es válida.");
        }

        if (delta == 0 || delta == long.MinValue)
        {
            return WalletAdjustmentResult.Failed("invalid_delta", "El ajuste debe ser distinto de cero.");
        }

        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 160)
        {
            return WalletAdjustmentResult.Failed("invalid_reference", "La referencia del ajuste no es válida.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            using var transaction = db.Database.BeginTransaction();
            var wallet = GetOrCreateWallet(db, accountId);
            var current = ToWallet(wallet);

            if (db.WalletTransactions.Any(row => row.Reference == reference))
            {
                return WalletAdjustmentResult.Failed(
                    "duplicate_reference",
                    "El ajuste ya fue aplicado.",
                    current);
            }

            if (delta > 0 && wallet.BalanceCredits > long.MaxValue - delta)
            {
                return WalletAdjustmentResult.Failed(
                    "wallet_limit",
                    "El saldo excedería el límite permitido.",
                    current);
            }

            if (delta < 0)
            {
                var available = Math.Max(0, wallet.BalanceCredits - wallet.ReservedCredits);
                var debit = -delta;
                if (debit > available)
                {
                    return WalletAdjustmentResult.Failed(
                        "insufficient_available",
                        $"No se pueden restar {debit} créditos: hay {available} disponibles.",
                        current);
                }
            }

            wallet.BalanceCredits = checked(wallet.BalanceCredits + delta);
            wallet.UpdatedAt = DateTimeOffset.UtcNow;
            db.WalletTransactions.Add(new WalletTransactionEntity
            {
                AccountId = accountId,
                Kind = EconomyTransactionKind.AdminAdjustment,
                AmountCredits = delta,
                BalanceAfterCredits = wallet.BalanceCredits,
                Reference = reference,
                CreatedAt = wallet.UpdatedAt
            });
            db.SaveChanges();
            transaction.Commit();

            return new WalletAdjustmentResult(
                true,
                "ok",
                delta > 0 ? "Saldo añadido." : "Saldo retirado.",
                ToWallet(wallet));
        }
    }

    public CatalogPage GetCatalogPage(
        int page,
        int pageSize,
        string? search = null,
        bool? active = null,
        StoreProductType? productType = null)
    {
        var boundedPage = Math.Clamp(page, 1, 100_000);
        var boundedPageSize = Math.Clamp(pageSize, 10, 100);
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var query = db.StoreCatalogItems.AsNoTracking();

        if (active.HasValue)
        {
            query = query.Where(item => item.Active == active.Value);
        }

        if (productType.HasValue)
        {
            query = query.Where(item => item.ProductType == productType.Value);
        }

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var hasNumericSearch = uint.TryParse(normalizedSearch, out var numericSearch);
            query = hasNumericSearch
                ? query.Where(item => item.ProductId == numericSearch
                    || item.DefIndex == numericSearch
                    || item.Name.Contains(normalizedSearch)
                    || item.Category.Contains(normalizedSearch))
                : query.Where(item => item.Name.Contains(normalizedSearch)
                    || item.Category.Contains(normalizedSearch)
                    || item.Description.Contains(normalizedSearch));
        }

        var totalCount = query.Count();
        var activeCount = db.StoreCatalogItems.Count(item => item.Active);
        var products = query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.ProductId)
            .Skip((boundedPage - 1) * boundedPageSize)
            .Take(boundedPageSize)
            .ToList();
        var productIds = products.Select(item => item.ProductId).ToArray();
        IReadOnlyList<StoreCatalogComponentEntity> components = productIds.Length == 0
            ? []
            : db.StoreCatalogComponents.AsNoTracking()
                .Where(component => productIds.Contains(component.ProductId))
                .ToList();

        return new CatalogPage(
            products.Select(item => ToCatalog(item, components)).ToArray(),
            totalCount,
            activeCount);
    }

    public IReadOnlyList<WalletTransactionSummary> GetTransactions(uint accountId, int limit = 50)
    {
        if (accountId == 0)
        {
            return [];
        }

        var boundedLimit = Math.Clamp(limit, 1, 100);
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        return db.WalletTransactions.AsNoTracking()
            .Where(row => row.AccountId == accountId)
            .OrderByDescending(row => row.Id)
            .Take(boundedLimit)
            .ToList()
            .Select(row => new WalletTransactionSummary(
                row.Id,
                row.Kind,
                row.AmountCredits,
                row.BalanceAfterCredits,
                row.Reference,
                row.CreatedAt))
            .ToArray();
    }

    public IReadOnlyList<CSOEconItem> GetItems(uint accountId)
    {
        if (accountId == 0)
        {
            return [];
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var rows = db.EconItems.AsNoTracking()
            .Where(item => item.AccountId == accountId)
            .ToList();
        return rows
            .OrderBy(item => item.Inventory)
            .ThenBy(item => item.ItemId)
            .Select(ToProto)
            .ToArray();
    }

    public CSOEconItem GrantItem(uint accountId, uint defIndex, uint quantity)
    {
        if (accountId == 0 || defIndex == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defIndex));
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var itemId = EconItemIdentity.ItemId(accountId, defIndex);
            var entity = db.EconItems.SingleOrDefault(item => item.ItemId == itemId);
            if (entity is null)
            {
                entity = NewItem(accountId, defIndex);
                db.EconItems.Add(entity);
            }

            entity.Quantity = quantity == 0 ? 1 : quantity;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
            return ToProto(entity);
        }
    }

    public bool SaveItem(uint accountId, CSOEconItem item)
    {
        if (accountId == 0 || item.AccountId != 0 && item.AccountId != accountId || item.DefIndex == 0)
        {
            return false;
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var itemId = item.Id != 0 ? item.Id : EconItemIdentity.ItemId(accountId, item.DefIndex);
            var entity = db.EconItems.SingleOrDefault(row => row.ItemId == itemId);
            if (entity is null)
            {
                entity = new EconItemEntity { ItemId = itemId, AccountId = accountId };
                db.EconItems.Add(entity);
            }

            Apply(entity, accountId, item);
            db.SaveChanges();
            return true;
        }
    }

    public StoreOperationResult BeginPurchase(uint accountId, IReadOnlyList<StorePurchaseLine> lines)
    {
        if (accountId == 0)
        {
            return StoreOperationResult.Failed("invalid_account", "La cuenta no es válida.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            using var transaction = db.Database.BeginTransaction();
            StoreOperationResult result;
            try
            {
                result = BeginPurchaseInternal(db, accountId, lines);
            }
            catch (OverflowException)
            {
                result = StoreOperationResult.Failed("invalid_purchase", "El importe de la compra es demasiado grande.");
            }
            if (result.Success)
            {
                transaction.Commit();
            }

            return result;
        }
    }

    public StoreOperationResult FinalizePurchase(uint accountId, ulong transactionId)
    {
        if (accountId == 0 || transactionId == 0 || transactionId > long.MaxValue)
        {
            return StoreOperationResult.Failed("invalid_transaction", "La transacción no es válida.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            using var transaction = db.Database.BeginTransaction();
            StoreOperationResult result;
            try
            {
                result = FinalizePurchaseInternal(db, accountId, (long)transactionId);
            }
            catch (OverflowException)
            {
                result = StoreOperationResult.Failed("invalid_purchase", "La compra no puede completarse porque excede los límites permitidos.");
            }
            if (result.Success || result.Code == "insufficient_funds")
            {
                transaction.Commit();
            }

            return result;
        }
    }

    public StoreOperationResult CancelPurchase(uint accountId, ulong transactionId)
    {
        if (accountId == 0 || transactionId == 0 || transactionId > long.MaxValue)
        {
            return StoreOperationResult.Failed("invalid_transaction", "La transacción no es válida.");
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            using var transaction = db.Database.BeginTransaction();
            var result = CancelPurchaseInternal(db, accountId, (long)transactionId);
            if (result.Success)
            {
                transaction.Commit();
            }

            return result;
        }
    }

    public int CancelPendingPurchases(uint accountId)
    {
        if (accountId == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            using var transaction = db.Database.BeginTransaction();
            var pending = db.StorePurchaseTransactions
                .Where(purchase => purchase.AccountId == accountId
                    && purchase.Status == StorePurchaseStatus.Pending)
                .ToList();
            var wallet = db.Wallets.SingleOrDefault(row => row.AccountId == accountId);
            if (pending.Count == 0 || wallet is null)
            {
                return 0;
            }

            foreach (var purchase in pending)
            {
                wallet.ReservedCredits = Math.Max(0, wallet.ReservedCredits - purchase.TotalCredits);
                purchase.Status = StorePurchaseStatus.Cancelled;
                purchase.CompletedAt = DateTimeOffset.UtcNow;
            }

            wallet.UpdatedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
            transaction.Commit();
            return pending.Count;
        }
    }

    public StoreOperationResult Purchase(uint accountId, IReadOnlyList<StorePurchaseLine> lines)
    {
        var started = BeginPurchase(accountId, lines);
        return !started.Success ? started : FinalizePurchase(accountId, started.TransactionId);
    }

    public bool UpsertCatalogItem(StoreCatalogItem item)
    {
        var result = ImportCatalog([item], preserveExisting: false);
        return result.ImportedCount + result.UpdatedCount == 1;
    }

    public CatalogImportSummary ImportCatalog(
        IReadOnlyList<StoreCatalogItem> items,
        bool preserveExisting)
    {
        if (items is null || items.Count == 0)
        {
            return new CatalogImportSummary(0, 0, 0);
        }

        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            using var transaction = db.Database.BeginTransaction();
            var now = DateTimeOffset.UtcNow;
            var existing = db.StoreCatalogItems.ToDictionary(row => row.ProductId);
            var seen = new HashSet<uint>();
            var imported = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var source in items)
            {
                if (!TryNormalizeCatalogItem(source, out var item)
                    || !seen.Add(item.ProductId))
                {
                    skipped++;
                    continue;
                }

                if (!existing.TryGetValue(item.ProductId, out var entity))
                {
                    entity = new StoreCatalogItemEntity
                    {
                        ProductId = item.ProductId,
                        CreatedAt = now,
                        Name = item.Name
                    };
                    db.StoreCatalogItems.Add(entity);
                    existing[item.ProductId] = entity;
                    imported++;
                }
                else
                {
                    if (preserveExisting && entity.ProductType != item.ProductType)
                    {
                        skipped++;
                        continue;
                    }

                    if (preserveExisting)
                    {
                        item = item with
                        {
                            ProductId = entity.ProductId,
                            PriceCredits = entity.PriceCredits,
                            Active = entity.Active
                        };
                    }

                    updated++;
                }

                ApplyCatalogEntity(db, entity, item, now);
            }

            db.SaveChanges();
            transaction.Commit();
            return new CatalogImportSummary(imported, updated, skipped);
        }
    }

    private static bool TryNormalizeCatalogItem(
        StoreCatalogItem source,
        out StoreCatalogItem normalized)
    {
        normalized = source;
        var components = source.Components ?? [];
        if (source.ProductId == 0 || string.IsNullOrWhiteSpace(source.Name)
            || source.PriceCredits <= 0
            || source.ProductType is not (StoreProductType.Item or StoreProductType.Set or StoreProductType.DotaPlusSubscription)
            || components.Any(component => component.ProductId == 0 || component.Quantity == 0))
        {
            return false;
        }

        var isDotaPlus = source.ProductType == StoreProductType.DotaPlusSubscription;
        if (isDotaPlus && (source.DotaPlusDays is < 1 or > 3650 || source.DefIndex != 0 || components.Count != 0)
            || !isDotaPlus && source.DotaPlusDays != 0)
        {
            return false;
        }

        var defIndex = source.ProductType == StoreProductType.Item
            ? source.DefIndex != 0 ? source.DefIndex : source.ProductId
            : 0;
        if (source.ProductType == StoreProductType.Item && components.Count != 0
            || source.ProductType == StoreProductType.Set && components.Count == 0
            || components.Any(component => component.ProductId == source.ProductId))
        {
            return false;
        }

        try
        {
            var normalizedComponents = components
                .GroupBy(component => component.ProductId)
                .Select(group => new StoreCatalogComponent(
                    group.Key,
                    checked((uint)group.Sum(value => (long)value.Quantity))))
                .ToArray();
            normalized = source with
            {
                DefIndex = defIndex,
                Name = source.Name.Trim(),
                Category = source.Category?.Trim() ?? string.Empty,
                Description = source.Description?.Trim() ?? string.Empty,
                DotaPlusDays = isDotaPlus ? source.DotaPlusDays : 0,
                Components = normalizedComponents
            };
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void ApplyCatalogEntity(
        D2stDbContext db,
        StoreCatalogItemEntity entity,
        StoreCatalogItem item,
        DateTimeOffset now)
    {
        entity.DefIndex = item.ProductType == StoreProductType.Item ? item.DefIndex : 0;
        entity.ProductType = item.ProductType;
        entity.PriceCredits = item.PriceCredits;
        entity.Name = item.Name;
        entity.Category = item.Category;
        entity.Description = item.Description;
        entity.BuildVersion = item.BuildVersion;
        entity.DotaPlusDays = item.ProductType == StoreProductType.DotaPlusSubscription
            ? item.DotaPlusDays
            : 0;
        entity.Active = item.Active;
        entity.UpdatedAt = now;

        var oldComponents = db.StoreCatalogComponents
            .Where(component => component.ProductId == item.ProductId)
            .ToList();
        db.StoreCatalogComponents.RemoveRange(oldComponents);
        foreach (var component in item.Components)
        {
            db.StoreCatalogComponents.Add(new StoreCatalogComponentEntity
            {
                ProductId = item.ProductId,
                ComponentProductId = component.ProductId,
                Quantity = component.Quantity
            });
        }
    }

    private static StoreOperationResult BeginPurchaseInternal(
        D2stDbContext db,
        uint accountId,
        IReadOnlyList<StorePurchaseLine> lines)
    {
        if (!TryNormalizeLines(lines, out var normalized, out var error))
        {
            return StoreOperationResult.Failed("invalid_purchase", error);
        }

        var products = db.StoreCatalogItems.ToDictionary(item => item.ProductId);
        var components = db.StoreCatalogComponents.ToList()
            .GroupBy(component => component.ProductId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var grants = new Dictionary<uint, uint>();
        var hasSubscription = false;
        var dotaPlusDays = 0;
        long total = 0;
        foreach (var line in normalized)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.Active)
            {
                return StoreOperationResult.Failed("product_unavailable", $"El producto {line.ProductId} no está disponible.");
            }

            if (product.PriceCredits <= 0 || line.Quantity > long.MaxValue / product.PriceCredits)
            {
                return StoreOperationResult.Failed("invalid_price", "El precio del producto no es válido.");
            }

            var lineTotal = product.PriceCredits * line.Quantity;
            if (total > long.MaxValue - lineTotal)
            {
                return StoreOperationResult.Failed("invalid_price", "El importe de la compra es demasiado grande.");
            }

            total += lineTotal;

            if (product.ProductType == StoreProductType.DotaPlusSubscription)
            {
                if (grants.Count != 0)
                {
                    return StoreOperationResult.Failed(
                        "mixed_purchase",
                        "Un plan Dota Plus no se puede combinar con items o sets.");
                }

                if (product.DotaPlusDays is < 1 or > 3650)
                {
                    return StoreOperationResult.Failed(
                        "invalid_product",
                        "El plan Dota Plus no tiene una duración válida.");
                }

                try
                {
                    dotaPlusDays = checked(dotaPlusDays + checked(product.DotaPlusDays * (int)line.Quantity));
                }
                catch (OverflowException)
                {
                    return StoreOperationResult.Failed(
                        "invalid_purchase",
                        "La duración total de Dota Plus es demasiado grande.");
                }

                if (dotaPlusDays > 3650)
                {
                    return StoreOperationResult.Failed(
                        "invalid_purchase",
                        "Una compra no puede añadir más de 3.650 días de Dota Plus.");
                }

                hasSubscription = true;
                continue;
            }

            if (hasSubscription)
            {
                return StoreOperationResult.Failed(
                    "mixed_purchase",
                    "Un plan Dota Plus no se puede combinar con items o sets.");
            }

            if (product.ProductType is not (StoreProductType.Item or StoreProductType.Set))
            {
                return StoreOperationResult.Failed("invalid_product", "El tipo de producto no es válido.");
            }

            if (!ExpandProduct(product.ProductId, line.Quantity, products, components, [], grants, out error))
            {
                return StoreOperationResult.Failed("invalid_product", error);
            }
        }

        if (total <= 0 || (grants.Count == 0 && dotaPlusDays == 0))
        {
            return StoreOperationResult.Failed("invalid_purchase", "La compra no contiene productos válidos.");
        }

        var wallet = GetOrCreateWallet(db, accountId);
        if (wallet.BalanceCredits < total
            || wallet.ReservedCredits < 0
            || wallet.ReservedCredits > wallet.BalanceCredits - total)
        {
            return StoreOperationResult.Failed("insufficient_funds", "Saldo insuficiente.", ToWallet(wallet));
        }

        if (wallet.ReservedCredits > long.MaxValue - total)
        {
            return StoreOperationResult.Failed("wallet_limit", "El saldo reservado excede el límite permitido.", ToWallet(wallet));
        }

        wallet.ReservedCredits = checked(wallet.ReservedCredits + total);
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        var purchase = new StorePurchaseTransactionEntity
        {
            AccountId = accountId,
            TotalCredits = total,
            DotaPlusDays = dotaPlusDays,
            Status = StorePurchaseStatus.Pending,
            LinesJson = JsonSerializer.Serialize(normalized),
            GrantsJson = JsonSerializer.Serialize(grants.Select(grant => new GrantLine(grant.Key, grant.Value))),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.StorePurchaseTransactions.Add(purchase);
        db.SaveChanges();

        return new StoreOperationResult(
            true,
            "ok",
            "Compra preparada.",
            (ulong)purchase.Id,
            [],
            [],
            ToWallet(wallet));
    }

    private static StoreOperationResult FinalizePurchaseInternal(
        D2stDbContext db,
        uint accountId,
        long transactionId)
    {
        var purchase = db.StorePurchaseTransactions
            .SingleOrDefault(row => row.Id == transactionId && row.AccountId == accountId);
        var wallet = db.Wallets.SingleOrDefault(row => row.AccountId == accountId);
        if (purchase is null || wallet is null)
        {
            return StoreOperationResult.Failed("transaction_not_found", "La transacción no existe.", WalletSnapshot.Empty(accountId));
        }

        if (purchase.Status == StorePurchaseStatus.Cancelled)
        {
            return StoreOperationResult.Failed("transaction_cancelled", "La transacción fue cancelada.", ToWallet(wallet));
        }

        if (purchase.Status == StorePurchaseStatus.Finalized)
        {
            var existingItemIds = Deserialize<ulong[]>(purchase.ItemIdsJson) ?? [];
            var existing = db.EconItems.AsNoTracking()
                .Where(item => item.AccountId == accountId && existingItemIds.Contains(item.ItemId))
                .ToList()
                .Select(ToProto)
                .ToArray();
            return new StoreOperationResult(true, "ok", "Compra completada.", (ulong)purchase.Id, existingItemIds, existing, ToWallet(wallet));
        }

        if (wallet.ReservedCredits < purchase.TotalCredits || wallet.BalanceCredits < purchase.TotalCredits)
        {
            wallet.ReservedCredits = Math.Max(0, wallet.ReservedCredits - purchase.TotalCredits);
            purchase.Status = StorePurchaseStatus.Cancelled;
            purchase.CompletedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
            return StoreOperationResult.Failed("insufficient_funds", "Saldo insuficiente.", ToWallet(wallet));
        }

        var grants = Deserialize<GrantLine[]>(purchase.GrantsJson) ?? [];
        if (purchase.DotaPlusDays is < 0 or > 3650)
        {
            return StoreOperationResult.Failed(
                "invalid_purchase",
                "La transacción contiene una duración Dota Plus no válida.",
                ToWallet(wallet));
        }

        if (purchase.DotaPlusDays > 0 && grants.Length != 0
            || purchase.DotaPlusDays == 0 && grants.Length == 0)
        {
            return StoreOperationResult.Failed(
                "invalid_purchase",
                "La transacción no contiene una concesión válida.",
                ToWallet(wallet));
        }

        var itemIds = new List<ulong>(grants.Length);
        var items = new List<CSOEconItem>(grants.Length);
        foreach (var grant in grants)
        {
            var itemId = EconItemIdentity.ItemId(accountId, grant.DefIndex);
            var entity = db.EconItems.SingleOrDefault(item => item.ItemId == itemId);
            if (entity is null)
            {
                entity = NewItem(accountId, grant.DefIndex);
                db.EconItems.Add(entity);
            }

            entity.Quantity = checked(entity.Quantity + grant.Quantity);
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            itemIds.Add(itemId);
            items.Add(ToProto(entity));
        }

        var now = DateTimeOffset.UtcNow;
        if (purchase.DotaPlusDays > 0)
        {
            var plus = db.DotaPlusAccounts.SingleOrDefault(row => row.AccountId == accountId);
            if (plus is null)
            {
                plus = new DotaPlusAccountEntity { AccountId = accountId };
                db.DotaPlusAccounts.Add(plus);
            }

            var isActive = plus.Enabled && plus.ExpiresAt is not null && plus.ExpiresAt > now;
            var baseDate = isActive ? plus.ExpiresAt!.Value : now;
            plus.Enabled = true;
            plus.StartedAt ??= now;
            plus.ExpiresAt = baseDate.AddDays(purchase.DotaPlusDays);
            plus.UpdatedAt = now;
            db.DotaPlusTransactions.Add(new DotaPlusTransactionEntity
            {
                AccountId = accountId,
                ChangedByAccountId = 0,
                Action = "purchase",
                Days = purchase.DotaPlusDays,
                Reason = $"Compra local #{purchase.Id}",
                ExpiresAtAfter = plus.ExpiresAt,
                CreatedAt = now
            });
        }

        wallet.BalanceCredits -= purchase.TotalCredits;
        wallet.ReservedCredits -= purchase.TotalCredits;
        wallet.UpdatedAt = now;
        db.WalletTransactions.Add(new WalletTransactionEntity
        {
            AccountId = accountId,
            Kind = EconomyTransactionKind.StorePurchase,
            AmountCredits = -purchase.TotalCredits,
            BalanceAfterCredits = wallet.BalanceCredits,
            Reference = $"store-purchase:{purchase.Id}",
            CreatedAt = now
        });
        purchase.Status = StorePurchaseStatus.Finalized;
        purchase.ItemIdsJson = JsonSerializer.Serialize(itemIds);
        purchase.CompletedAt = now;
        db.SaveChanges();

        return new StoreOperationResult(true, "ok", "Compra completada.", (ulong)purchase.Id, itemIds, items, ToWallet(wallet));
    }

    private static StoreOperationResult CancelPurchaseInternal(
        D2stDbContext db,
        uint accountId,
        long transactionId)
    {
        var purchase = db.StorePurchaseTransactions
            .SingleOrDefault(row => row.Id == transactionId && row.AccountId == accountId);
        var wallet = db.Wallets.SingleOrDefault(row => row.AccountId == accountId);
        if (purchase is null || wallet is null)
        {
            return StoreOperationResult.Failed("transaction_not_found", "La transacción no existe.", WalletSnapshot.Empty(accountId));
        }

        if (purchase.Status == StorePurchaseStatus.Finalized)
        {
            return StoreOperationResult.Failed("transaction_finalized", "La compra ya fue completada.", ToWallet(wallet));
        }

        if (purchase.Status == StorePurchaseStatus.Pending)
        {
            wallet.ReservedCredits = Math.Max(0, wallet.ReservedCredits - purchase.TotalCredits);
            wallet.UpdatedAt = DateTimeOffset.UtcNow;
            purchase.Status = StorePurchaseStatus.Cancelled;
            purchase.CompletedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
        }

        return new StoreOperationResult(true, "ok", "Compra cancelada.", (ulong)purchase.Id, [], [], ToWallet(wallet));
    }

    private static bool TryNormalizeLines(
        IReadOnlyList<StorePurchaseLine> lines,
        out IReadOnlyList<StorePurchaseLine> normalized,
        out string error)
    {
        normalized = [];
        error = string.Empty;
        if (lines is null || lines.Count == 0 || lines.Count > 32)
        {
            error = "La compra debe contener entre 1 y 32 productos.";
            return false;
        }

        try
        {
            var grouped = lines
                .Where(line => line.ProductId != 0 && line.Quantity != 0)
                .GroupBy(line => line.ProductId)
                .Select(group =>
                {
                    var quantity = group.Sum(line => (long)line.Quantity);
                    return new StorePurchaseLine(group.Key, checked((uint)quantity));
                })
                .ToArray();
            if (grouped.Length == 0 || grouped.Any(line => line.Quantity > 100))
            {
                error = "La cantidad solicitada no es válida.";
                return false;
            }

            normalized = grouped;
            return true;
        }
        catch (OverflowException)
        {
            error = "La cantidad solicitada no es válida.";
            return false;
        }
    }

    private static bool ExpandProduct(
        uint productId,
        uint quantity,
        IReadOnlyDictionary<uint, StoreCatalogItemEntity> products,
        IReadOnlyDictionary<uint, StoreCatalogComponentEntity[]> components,
        HashSet<uint> stack,
        IDictionary<uint, uint> grants,
        out string error)
    {
        error = string.Empty;
        if (quantity == 0 || quantity > 1000 || !products.TryGetValue(productId, out var product))
        {
            error = $"El producto {productId} no puede entregarse.";
            return false;
        }

        if (!stack.Add(productId))
        {
            error = "El set contiene una referencia circular.";
            return false;
        }

        try
        {
            if (product.ProductType == StoreProductType.Item)
            {
                if (product.DefIndex == 0)
                {
                    error = $"El item {productId} no tiene DefIndex.";
                    return false;
                }

                if (!grants.TryGetValue(product.DefIndex, out var current))
                {
                    grants[product.DefIndex] = quantity;
                }
                else
                {
                    grants[product.DefIndex] = checked(current + quantity);
                }

                return grants.Count <= 256;
            }

            if (!components.TryGetValue(productId, out var children) || children.Length == 0)
            {
                error = $"El set {productId} no tiene componentes.";
                return false;
            }

            foreach (var child in children)
            {
                var childQuantity = checked(quantity * child.Quantity);
                if (!ExpandProduct(child.ComponentProductId, childQuantity, products, components, stack, grants, out error))
                {
                    return false;
                }
            }

            return true;
        }
        catch (OverflowException)
        {
            error = "La cantidad total del set es demasiado grande.";
            return false;
        }
        finally
        {
            stack.Remove(productId);
        }
    }

    private static WalletEntity GetOrCreateWallet(D2stDbContext db, uint accountId)
    {
        var wallet = db.Wallets.SingleOrDefault(row => row.AccountId == accountId);
        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new WalletEntity
        {
            AccountId = accountId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Wallets.Add(wallet);
        return wallet;
    }

    private static EconItemEntity NewItem(uint accountId, uint defIndex) => new()
    {
        ItemId = EconItemIdentity.ItemId(accountId, defIndex),
        AccountId = accountId,
        DefIndex = defIndex,
        Quantity = 0,
        Level = 1,
        Quality = 4,
        Origin = 2,
        Inventory = 1,
        OriginalId = EconItemIdentity.ItemId(accountId, defIndex),
        EquippedStatesJson = "[]",
        AttributesJson = "[]",
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static void Apply(EconItemEntity entity, uint accountId, CSOEconItem item)
    {
        entity.AccountId = accountId;
        entity.DefIndex = item.DefIndex;
        entity.Quantity = item.Quantity;
        entity.Level = item.Level;
        entity.Quality = item.Quality;
        entity.Flags = item.Flags;
        entity.Origin = item.Origin;
        entity.Inventory = item.Inventory;
        entity.Style = item.Style;
        entity.OriginalId = item.OriginalId != 0 ? item.OriginalId : entity.ItemId;
        entity.EquippedStatesJson = JsonSerializer.Serialize(
            item.EquippedStates.Select(state => new EquippedState(state.NewClass, state.NewSlot)));
        entity.AttributesJson = JsonSerializer.Serialize(
            item.Attributes.Select(attribute => new ItemAttribute(
                attribute.DefIndex,
                attribute.Value,
                attribute.ValueBytes)));
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static CSOEconItem ToProto(EconItemEntity entity)
    {
        var item = new CSOEconItem
        {
            Id = entity.ItemId,
            AccountId = entity.AccountId,
            DefIndex = entity.DefIndex,
            Quantity = entity.Quantity,
            Level = entity.Level,
            Quality = entity.Quality,
            Flags = entity.Flags,
            Origin = entity.Origin,
            Inventory = entity.Inventory,
            Style = entity.Style,
            OriginalId = entity.OriginalId != 0 ? entity.OriginalId : entity.ItemId
        };

        foreach (var state in Deserialize<EquippedState[]>(entity.EquippedStatesJson) ?? [])
        {
            item.EquippedStates.Add(new CSOEconItemEquipped { NewClass = state.NewClass, NewSlot = state.NewSlot });
        }

        foreach (var attribute in Deserialize<ItemAttribute[]>(entity.AttributesJson) ?? [])
        {
            item.Attributes.Add(new CSOEconItemAttribute
            {
                DefIndex = attribute.DefIndex,
                Value = attribute.Value,
                ValueBytes = attribute.ValueBytes
            });
        }

        return item;
    }

    private static StoreCatalogItem ToCatalog(
        StoreCatalogItemEntity item,
        IEnumerable<StoreCatalogComponentEntity> components) =>
        new(
            item.ProductId,
            item.DefIndex,
            item.Name,
            item.ProductType,
            item.PriceCredits,
            item.Category,
            item.Description,
            item.BuildVersion,
            item.DotaPlusDays,
            item.Active,
            components
                .Where(component => component.ProductId == item.ProductId)
                .Select(component => new StoreCatalogComponent(component.ComponentProductId, component.Quantity))
                .ToArray());

    private static WalletSnapshot ToWallet(WalletEntity wallet) =>
        new(wallet.AccountId, wallet.BalanceCredits, wallet.ReservedCredits, wallet.UpdatedAt);

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private sealed record GrantLine(uint DefIndex, uint Quantity);

    private sealed record EquippedState(uint NewClass, uint NewSlot);

    private sealed record ItemAttribute(uint DefIndex, uint Value, byte[]? ValueBytes);
}
