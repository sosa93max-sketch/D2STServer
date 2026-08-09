using D2ST.Api.Contracts;
using D2ST.Api.Economy;
using D2ST.Core.Economy;
using D2ST.GameCoordinator.Econ;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.Persistence;
using D2ST.Steam;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Endpoints;

public static class StoreEndpoints
{
    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/store/catalog", (
            HttpContext http,
            ISessionStore sessions,
            IEconomyStore economy) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(ToCatalogResponse(
                economy.GetCatalog(),
                economy.GetItems(session.Account.AccountId)));
        });

        app.MapGet("/api/store/catalog/page", (
            HttpContext http,
            ISessionStore sessions,
            IEconomyStore economy,
            int page = 1,
            int pageSize = 24,
            string? search = null,
            string? category = null,
            string? hero = null,
            int? type = null) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            if (type.HasValue && type.Value is not (0 or 1 or 2))
            {
                return Results.BadRequest(new AdminMessageResponse("El filtro de tipo no es válido."));
            }

            var result = economy.GetCatalogPage(
                page,
                pageSize,
                search,
                active: true,
                type.HasValue ? (StoreProductType)type.Value : null,
                category,
                hero);
            var filters = economy.GetCatalogFilters();
            return Results.Ok(new StoreCatalogPageResponse(
                ToCatalogResponse(result.Items, economy.GetItems(session.Account.AccountId)),
                Math.Clamp(page, 1, 100_000),
                Math.Clamp(pageSize, 10, 100),
                result.TotalCount,
                result.ActiveCount,
                filters.Categories,
                filters.Heroes));
        });

        app.MapGet("/api/store/wallet", (
            HttpContext http,
            ISessionStore sessions,
            IEconomyStore economy) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(ToWalletResponse(economy.GetWallet(session.Account.AccountId)));
        });

        app.MapGet("/api/store/transactions", (
            HttpContext http,
            ISessionStore sessions,
            IEconomyStore economy,
            int limit = 50) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(economy.GetTransactions(session.Account.AccountId, limit));
        });

        app.MapGet("/api/store/inventory", (
            HttpContext http,
            ISessionStore sessions,
            EconInventory inventory) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var items = inventory.Items(session.Account.SteamId);
            return Results.Ok(new GcInventoryResponse(
                items.Select(ToInventoryItem).ToArray(),
                inventory.CacheVersion(session.Account.SteamId)));
        });

        app.MapPost("/api/store/inventory/equip", (
            StoreEquipRequest request,
            HttpContext http,
            ISessionStore sessions,
            EconInventory inventory) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            if (request.ItemId == 0 || request.HeroId == 0)
            {
                return Results.BadRequest(new AdminMessageResponse("El artículo o el héroe no son válidos."));
            }

            if (!inventory.TryGetItem(session.Account.SteamId, request.ItemId, out _))
            {
                return Results.NotFound(new AdminMessageResponse("El artículo no pertenece a esta cuenta."));
            }

            var changed = inventory.Equip(
                session.Account.SteamId,
                [new CMsgAdjustItemEquippedState
                {
                    ItemId = request.ItemId,
                    NewClass = request.HeroId,
                    NewSlot = request.Slot,
                    StyleIndex = request.StyleIndex
                }]);

            return Results.Ok(new StoreEquipResponse(
                changed > 0,
                changed,
                inventory.CacheVersion(session.Account.SteamId),
                changed > 0 ? "ok" : "unchanged",
                changed > 0 ? "Artículo equipado." : "El artículo ya estaba equipado."));
        });

        app.MapGet("/api/admin/store/catalog", async (
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration configuration,
            IEconomyStore economy,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAdminAsync(
                http, sessions, db, configuration, cancellationToken);
            if (!authorization.Authenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Ok(ToCatalogResponse(economy.GetCatalog(false), []));
        });

        app.MapGet("/api/admin/store/catalog/page", async (
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration configuration,
            IEconomyStore economy,
            int page = 1,
            int pageSize = 50,
            string? search = null,
            string status = "all",
            int? type = null,
            CancellationToken cancellationToken = default) =>
        {
            var authorization = await AuthorizeAdminAsync(
                http, sessions, db, configuration, cancellationToken);
            if (!authorization.Authenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var normalizedStatus = status.Trim().ToLowerInvariant();
            if (normalizedStatus is not ("all" or "active" or "inactive"))
            {
                return Results.BadRequest(new AdminMessageResponse("El filtro de estado no es válido."));
            }

            if (type.HasValue && type.Value is not (0 or 1 or 2))
            {
                return Results.BadRequest(new AdminMessageResponse("El filtro de tipo no es válido."));
            }

            var active = normalizedStatus switch
            {
                "active" => true,
                "inactive" => false,
                _ => (bool?)null
            };
            var productType = type.HasValue ? (StoreProductType)type.Value : (StoreProductType?)null;
            var result = economy.GetCatalogPage(page, pageSize, search, active, productType);
            var boundedPage = Math.Clamp(page, 1, 100_000);
            var boundedPageSize = Math.Clamp(pageSize, 10, 100);
            return Results.Ok(new AdminCatalogPageResponse(
                ToCatalogResponse(result.Items, []),
                boundedPage,
                boundedPageSize,
                result.TotalCount,
                result.ActiveCount));
        });

        app.MapPost("/api/admin/store/catalog/clear", async (
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration configuration,
            IEconomyStore economy,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAdminAsync(
                http, sessions, db, configuration, cancellationToken);
            if (!authorization.Authenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var removed = economy.ClearCatalog();
            return Results.Ok(new StoreCatalogClearResponse(
                removed,
                removed == 0
                    ? "El catálogo ya estaba vacío."
                    : $"Se eliminaron {removed} productos del catálogo. El inventario de los usuarios se conservó."));
        });

        app.MapPost("/api/admin/users/{accountId:long}/wallet/adjust", async (
            long accountId,
            AdminWalletAdjustRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration configuration,
            IEconomyStore economy,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAdminAsync(
                http, sessions, db, configuration, cancellationToken);
            if (!authorization.Authenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (accountId <= 0 || accountId > uint.MaxValue)
            {
                return Results.BadRequest(new AdminMessageResponse("La cuenta no es válida."));
            }

            var account = await db.Accounts.AsNoTracking()
                .SingleOrDefaultAsync(row => row.AccountId == (uint)accountId, cancellationToken);
            if (account is null)
            {
                return Results.NotFound(new AdminMessageResponse("Usuario no encontrado."));
            }

            var session = http.Authenticate(sessions)!;
            var reason = CleanReason(request.Reason);
            var reference = $"admin-adjustment:{session.Account.AccountId}:{Guid.NewGuid():N}:{reason}";
            var result = economy.AdjustWallet((uint)accountId, request.DeltaDollars, reference);
            var response = new AdminWalletAdjustResponse(
                result.Success,
                result.Code,
                result.Message,
                ToWalletResponse(result.Wallet));
            if (result.Success)
            {
                return Results.Ok(response);
            }

            var statusCode = result.Code is "insufficient_available" or "wallet_limit"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return Results.Json(response, statusCode: statusCode);
        });

        app.MapPost("/api/store/purchase", (
            StorePurchaseRequest request,
            HttpContext http,
            ISessionStore sessions,
            EconInventory inventory,
            DotaPlusProjection dotaPlus) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var lines = request.Lines is { Count: > 0 }
                ? request.Lines
                    .Select(line => new StorePurchaseLine(line.ProductId, line.Quantity))
                    .ToArray()
                : request.ProductId != 0
                    ? [new StorePurchaseLine(request.ProductId, request.Quantity)]
                    : Array.Empty<StorePurchaseLine>();
            var result = inventory.Purchase(session.Account.AccountId, session.Account.SteamId, lines);
            if (result.Success)
            {
                dotaPlus.Refresh(session.Account.AccountId);
            }
            var response = ToPurchaseResponse(result);
            return result.Success
                ? Results.Ok(response)
                : Results.Json(response, statusCode: result.Code == "insufficient_funds" ? 409 : 400);
        });

        app.MapPost("/api/admin/store/catalog", async (
            StoreCatalogUpsertRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration configuration,
            IEconomyStore economy,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var account = await db.Accounts.AsNoTracking()
                .SingleOrDefaultAsync(row => row.AccountId == session.Account.AccountId, cancellationToken);
            var admins = configuration.GetSection("Admin:Usernames").Get<List<string>>() ?? [];
            if (account is null || !admins.Contains(account.Username, StringComparer.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var item = new StoreCatalogItem(
                request.ProductId,
                request.DefIndex,
                request.Name,
                request.ProductType,
                request.PriceDollars,
                request.Category ?? string.Empty,
                request.Description ?? string.Empty,
                request.BuildVersion,
                request.DotaPlusDays,
                request.Active,
                (request.Components ?? [])
                    .Select(component => new StoreCatalogComponent(component.ProductId, component.Quantity))
                    .ToArray(),
                request.MarketHashName ?? string.Empty,
                request.MarketSearchName ?? string.Empty,
                request.MarketLowestPriceCents,
                request.MarketMedianPriceCents,
                request.MarketVolume,
                request.MarketPriceSource ?? string.Empty,
                request.MarketPriceStatus ?? "not_checked",
                request.MarketPriceUpdatedAt,
                request.Heroes);
            if (!economy.UpsertCatalogItem(item))
            {
                return Results.BadRequest(new AdminMessageResponse("El producto o sus componentes no son válidos."));
            }

            var stored = ToCatalogResponse(economy.GetCatalog(false), [])
                .FirstOrDefault(candidate => candidate.ProductId == item.ProductId
                    || item.DefIndex != 0
                    && candidate.ProductType == item.ProductType
                    && candidate.DefIndex == item.DefIndex);
            return Results.Ok(stored);
        });

        app.MapPost("/api/admin/store/catalog/discover", async (
            DotaCatalogDiscoverRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration configuration,
            DotaCatalogImporter importer,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAdminAsync(
                http, sessions, db, configuration, cancellationToken);
            if (!authorization.Authenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            try
            {
                var source = importer.Read(request.DotaPath, request.Language);
                var items = source.Items.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var search = request.Search.Trim();
                    items = items.Where(item =>
                        item.DefIndex.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        item.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        item.Slot.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        item.Prefab.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        item.HeroNames.Any(hero => hero.Contains(search, StringComparison.OrdinalIgnoreCase)));
                }

                var take = Math.Clamp(request.Take, 25, 1000);
                var selected = items.Take(take).Select(ToDotaDefinitionResponse).ToArray();
                return Results.Ok(new DotaCatalogDiscoverResponse(
                    source.DotaPath,
                    source.PakPath,
                    source.SteamInfPath,
                    source.ClientVersion,
                    source.ParsedDefinitionCount,
                    source.Items.Count,
                    selected));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return Results.BadRequest(new AdminMessageResponse(exception.Message));
            }
        });

        app.MapPost("/api/admin/store/catalog/import", async (
            DotaCatalogImportRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration configuration,
            IEconomyStore economy,
            DotaCatalogImporter importer,
            IMarketPriceRefreshQueue marketPriceQueue,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAdminAsync(
                http, sessions, db, configuration, cancellationToken);
            if (!authorization.Authenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.DefaultPriceDollars < 0
                || request.DefaultPriceDollars > LocalEconomyCurrency.MaxWireDollars)
            {
                return Results.BadRequest(new AdminMessageResponse(
                    $"DefaultPriceDollars debe estar entre 0 y {LocalEconomyCurrency.MaxWireDollars}. Usa 0 para esperar el precio real de Steam."));
            }

            try
            {
                var source = importer.Read(request.DotaPath, request.Language);
                var selectedIndexes = request.DefIndexes is { Count: > 0 }
                    ? request.DefIndexes.ToHashSet()
                    : null;
                var definitions = selectedIndexes is null
                    ? source.Items
                    : source.Items.Where(item => selectedIndexes.Contains(item.DefIndex)).ToArray();
                var existing = economy.GetCatalog(false);
                var buildVersion = request.BuildVersion ?? source.ClientVersion;
                var products = definitions.Select(definition =>
                {
                    // A replacement import is intentionally a clean slate:
                    // use the requested default price/activation instead of
                    // carrying values from the catalog that is about to be
                    // deleted. A normal import keeps existing business data.
                    var current = request.ClearExisting
                        ? null
                        : existing.FirstOrDefault(item =>
                            item.ProductType == D2ST.Core.Economy.StoreProductType.Item &&
                            (item.ProductId == definition.DefIndex || item.DefIndex == definition.DefIndex));
                    var productId = current?.ProductId ?? definition.DefIndex;
                    var preservePrice = current is not null
                        && (string.Equals(current.MarketPriceStatus, "matched", StringComparison.OrdinalIgnoreCase)
                            && (current.MarketLowestPriceCents is > 0 || current.MarketMedianPriceCents is > 0)
                            || string.Equals(current.MarketPriceSource, "manual", StringComparison.OrdinalIgnoreCase)
                            && current.PriceDollars > 0);
                    var price = preservePrice ? current!.PriceDollars : request.DefaultPriceDollars;
                    var active = preservePrice ? current!.Active : request.Activate && price > 0;
                    var category = string.IsNullOrWhiteSpace(definition.Slot)
                        ? definition.Prefab
                        : definition.Slot;
                    return new StoreCatalogItem(
                        productId,
                        definition.DefIndex,
                        string.IsNullOrWhiteSpace(definition.DisplayName)
                            ? definition.Name
                            : definition.DisplayName,
                        D2ST.Core.Economy.StoreProductType.Item,
                        price,
                        category,
                        definition.Description,
                        buildVersion,
                        0,
                        active,
                        [],
                        MarketSearchName: definition.MarketSearchName,
                        HeroNames: definition.HeroNames);
                }).ToArray();
                var removedExisting = 0;
                CatalogImportSummary result;
                if (request.ClearExisting)
                {
                    // The source has already been fully read and validated,
                    // so replacing the catalog cannot leave an installation
                    // empty because of a bad VPK path.
                    removedExisting = economy.ClearCatalog();
                    result = economy.ImportCatalog(products, preserveExisting: false);
                }
                else
                {
                    result = economy.ImportCatalog(products, preserveExisting: true);
                }
                var pricesQueued = marketPriceQueue.Enqueue(
                    products
                        .Where(product => product.ProductType == D2ST.Core.Economy.StoreProductType.Item)
                        .Select(product => product.ProductId),
                    request.Activate);
                var activationMessage = request.Activate
                    ? "Los productos nuevos con precio válido se activarán al terminar la consulta Steam."
                    : "Los productos nuevos quedaron desactivados hasta que el administrador los active.";
                return Results.Ok(new DotaCatalogImportResponse(
                    source.DotaPath,
                    source.PakPath,
                    source.SteamInfPath,
                    source.ClientVersion,
                    source.ParsedDefinitionCount,
                    source.Items.Count,
                    result.ImportedCount,
                    result.UpdatedCount,
                    result.SkippedCount,
                    request.DefaultPriceDollars,
                    request.Activate,
                    $"Catálogo importado: {result.ImportedCount} nuevos, {result.UpdatedCount} actualizados, {result.SkippedCount} omitidos. {activationMessage} Se programó la consulta de precios reales para {pricesQueued} artículos.",
                    removedExisting,
                    source.Language,
                    pricesQueued));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return Results.BadRequest(new AdminMessageResponse(exception.Message));
            }
        });

        app.MapPost("/api/admin/store/catalog/market-prices", async (
            MarketPriceSyncRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration configuration,
            SteamMarketPriceSync marketPrices,
            CancellationToken cancellationToken) =>
        {
            var authorization = await AuthorizeAdminAsync(
                http, sessions, db, configuration, cancellationToken);
            if (!authorization.Authenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.Authorized)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            try
            {
                return Results.Ok(await marketPrices.SyncAsync(request, cancellationToken));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return Results.BadRequest(new AdminMessageResponse(exception.Message));
            }
        });

        return app;
    }

    private static async Task<AdminAuthorization> AuthorizeAdminAsync(
        HttpContext http,
        ISessionStore sessions,
        D2stDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var session = http.Authenticate(sessions);
        if (session is null)
        {
            return new AdminAuthorization(false, false);
        }

        var account = await db.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(row => row.AccountId == session.Account.AccountId, cancellationToken);
        var admins = configuration.GetSection("Admin:Usernames").Get<List<string>>() ?? [];
        return new AdminAuthorization(
            true,
            account is not null && admins.Contains(account.Username, StringComparer.OrdinalIgnoreCase));
    }

    private static string CleanReason(string? reason)
    {
        var normalized = new string((reason ?? "ajuste manual")
            .Where(character => !char.IsControl(character))
            .ToArray())
            .Trim();
        if (normalized.Length == 0)
        {
            normalized = "ajuste manual";
        }

        return normalized.Length <= 80 ? normalized : normalized[..80];
    }

    private static DotaCatalogDefinitionResponse ToDotaDefinitionResponse(DotaCatalogDefinition item) =>
        new(
            item.DefIndex,
            item.Name,
            item.DisplayName,
            item.MarketSearchName,
            item.ItemName,
            item.Description,
            item.Prefab,
            item.Slot,
            item.Quality,
            item.Rarity,
            item.ImageInventory,
            item.HeroNames);

    private sealed record AdminAuthorization(bool Authenticated, bool Authorized);

    private static IReadOnlyList<StoreCatalogItemResponse> ToCatalogResponse(
        IReadOnlyList<StoreCatalogItem> catalog,
        IReadOnlyList<global::CSOEconItem> owned)
    {
        var quantities = owned
            .GroupBy(item => item.DefIndex)
            .ToDictionary(
                group => group.Key,
                group => checked((uint)Math.Min(uint.MaxValue, group.Sum(item => (long)item.Quantity))));
        return catalog.Select(item => new StoreCatalogItemResponse(
            item.ProductId,
            item.DefIndex,
            item.Name,
            item.ProductType,
            item.PriceDollars,
            item.Category,
            item.Description,
            item.BuildVersion,
            item.DotaPlusDays,
            item.Active,
            item.Components,
            item.ProductType == D2ST.Core.Economy.StoreProductType.Item
                && quantities.TryGetValue(item.DefIndex, out var quantity)
                ? quantity
                : 0,
            item.MarketHashName,
            item.MarketSearchName,
            item.MarketLowestPriceCents,
            item.MarketMedianPriceCents,
            item.MarketVolume,
            item.MarketPriceSource,
            item.MarketPriceStatus,
            item.MarketPriceUpdatedAt,
            item.HeroNames ?? [])).ToArray();
    }

    private static StoreWalletResponse ToWalletResponse(WalletSnapshot wallet) =>
        new(
            wallet.AccountId,
            wallet.BalanceDollars,
            wallet.ReservedDollars,
            wallet.AvailableDollars,
            wallet.UpdatedAt);

    private static StorePurchaseResponse ToPurchaseResponse(StoreOperationResult result) =>
        new(
            result.Success,
            result.Code,
            result.Message,
            result.TransactionId,
            result.ItemIds,
            ToWalletResponse(result.Wallet),
            result.Items.Select(ToInventoryItem).ToArray());

    private static GcInventoryItem ToInventoryItem(global::CSOEconItem item) =>
        new(item.Id, item.DefIndex, item.Quantity, item.Style, item.Inventory);
}
