using D2ST.Api.Contracts;
using D2ST.GameCoordinator.Econ;
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

        app.MapPost("/api/store/purchase", (
            StorePurchaseRequest request,
            HttpContext http,
            ISessionStore sessions,
            EconInventory inventory) =>
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
                request.PriceCredits,
                request.Category ?? string.Empty,
                request.Description ?? string.Empty,
                request.BuildVersion,
                request.Active,
                (request.Components ?? [])
                    .Select(component => new StoreCatalogComponent(component.ProductId, component.Quantity))
                    .ToArray());
            return economy.UpsertCatalogItem(item)
                ? Results.Ok(ToCatalogResponse(economy.GetCatalog(false), []).FirstOrDefault(candidate => candidate.ProductId == item.ProductId))
                : Results.BadRequest(new AdminMessageResponse("El producto o sus componentes no son válidos."));
        });

        return app;
    }

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
            item.PriceCredits,
            item.Category,
            item.Description,
            item.BuildVersion,
            item.Active,
            item.Components,
            item.ProductType == D2ST.Core.Economy.StoreProductType.Item
                && quantities.TryGetValue(item.DefIndex, out var quantity)
                ? quantity
                : 0)).ToArray();
    }

    private static StoreWalletResponse ToWalletResponse(WalletSnapshot wallet) =>
        new(
            wallet.AccountId,
            wallet.BalanceCredits,
            wallet.ReservedCredits,
            wallet.AvailableCredits,
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
