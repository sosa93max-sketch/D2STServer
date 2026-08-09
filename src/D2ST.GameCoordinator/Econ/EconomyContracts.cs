using D2ST.Core.Economy;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Econ;

/// <summary>
/// Wire-format settings shared by the native Dota store welcome and sales
/// responses. The local economy stores whole USD dollars internally and turns
/// them into the protocol's USD minor units only at the native-client boundary.
/// Therefore one local dollar is sent as 100 wire units and renders as $1.00.
/// </summary>
public static class LocalEconomyCurrency
{
    // ECurrencyCode::USD in the Steam/Dota protocol.
    public const uint Code = 1;
    public const string CountryCode = "US";
    public const long MinorUnitsPerDollar = 100;
    public const long MaxWireDollars = uint.MaxValue / MinorUnitsPerDollar;

    public static uint ToWireAmount(long dollars)
    {
        if (dollars <= 0)
        {
            return 0;
        }

        return dollars > MaxWireDollars
            ? uint.MaxValue
            : (uint)(dollars * MinorUnitsPerDollar);
    }
}

/// <summary>
/// Item definitions hard-coded by the native Dota Plus checkout in the target
/// client. A local plan keeps its own ProductId, but the client sends one of
/// these definitions when the user opens the native subscription checkout.
/// </summary>
public static class DotaPlusNativeSkus
{
    public static IReadOnlyList<uint> All { get; } =
    [
        19994,
        19995,
        19996,
        19997,
        19998,
        19999
    ];

    public static bool Contains(uint itemDefId) => All.Contains(itemDefId);
}

/// <summary>
/// The store protobuf uses a legacy result field whose successful value is 1.
/// It is not the generic <see cref="EGCMsgResponse"/> enum, where OK is 0.
/// </summary>
public static class StorePurchaseWireResult
{
    public const int Failure = 0;
    public const int Success = 1;
}

public sealed record StoreCatalogComponent(uint ProductId, uint Quantity);

public sealed record StoreCatalogItem(
    uint ProductId,
    uint DefIndex,
    string Name,
    StoreProductType ProductType,
    long PriceDollars,
    string Category,
    string Description,
    uint BuildVersion,
    int DotaPlusDays,
    bool Active,
    IReadOnlyList<StoreCatalogComponent> Components,
    string MarketHashName = "",
    long? MarketLowestPriceCents = null,
    long? MarketMedianPriceCents = null,
    long? MarketVolume = null,
    string MarketPriceSource = "",
    string MarketPriceStatus = "not_checked",
    DateTimeOffset? MarketPriceUpdatedAt = null,
    IReadOnlyList<string>? HeroNames = null);

public sealed record WalletSnapshot(
    uint AccountId,
    long BalanceDollars,
    long ReservedDollars,
    DateTimeOffset? UpdatedAt)
{
    public long AvailableDollars => Math.Max(0, BalanceDollars - ReservedDollars);

    public static WalletSnapshot Empty(uint accountId) => new(accountId, 0, 0, null);
}

public sealed record WalletAdjustmentResult(
    bool Success,
    string Code,
    string Message,
    WalletSnapshot Wallet)
{
    public static WalletAdjustmentResult Failed(
        string code,
        string message,
        WalletSnapshot? wallet = null) =>
        new(false, code, message, wallet ?? WalletSnapshot.Empty(0));
}

public sealed record WalletTransactionSummary(
    long Id,
    EconomyTransactionKind Kind,
    long AmountDollars,
    long BalanceAfterDollars,
    string Reference,
    DateTimeOffset CreatedAt);

public sealed record StorePurchaseLine(uint ProductId, uint Quantity);

public sealed record StoreOperationResult(
    bool Success,
    string Code,
    string Message,
    ulong TransactionId,
    IReadOnlyList<ulong> ItemIds,
    IReadOnlyList<CSOEconItem> Items,
    WalletSnapshot Wallet)
{
    public static StoreOperationResult Failed(
        string code,
        string message,
        WalletSnapshot? wallet = null) =>
        new(false, code, message, 0, [], [], wallet ?? WalletSnapshot.Empty(0));
}

public sealed record CatalogImportSummary(
    int ImportedCount,
    int UpdatedCount,
    int SkippedCount);

public sealed record CatalogPage(
    IReadOnlyList<StoreCatalogItem> Items,
    int TotalCount,
    int ActiveCount);

public sealed record CatalogFilters(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Heroes);

/// <summary>
/// Persistence boundary for the local wallet, catalog and econ inventory. The
/// GC project consumes this contract without taking a dependency on EF Core.
/// </summary>
public interface IEconomyStore
{
    IReadOnlyList<StoreCatalogItem> GetCatalog(bool activeOnly = true);

    StoreCatalogItem? FindProduct(uint productIdOrDefIndex, bool activeOnly = true);

    WalletSnapshot GetWallet(uint accountId);

    WalletAdjustmentResult AdjustWallet(uint accountId, long delta, string reference);

    IReadOnlyList<WalletTransactionSummary> GetTransactions(uint accountId, int limit = 50);

    IReadOnlyList<CSOEconItem> GetItems(uint accountId);

    CSOEconItem GrantItem(uint accountId, uint defIndex, uint quantity);

    bool SaveItem(uint accountId, CSOEconItem item);

    StoreOperationResult BeginPurchase(uint accountId, IReadOnlyList<StorePurchaseLine> lines);

    StoreOperationResult FinalizePurchase(uint accountId, ulong transactionId);

    StoreOperationResult CancelPurchase(uint accountId, ulong transactionId);

    int CancelPendingPurchases(uint accountId);

    StoreOperationResult Purchase(uint accountId, IReadOnlyList<StorePurchaseLine> lines);

    bool UpsertCatalogItem(StoreCatalogItem item);

    CatalogPage GetCatalogPage(
        int page,
        int pageSize,
        string? search = null,
        bool? active = null,
        StoreProductType? productType = null,
        string? category = null,
        string? hero = null);

    CatalogFilters GetCatalogFilters(bool activeOnly = true);

    int ClearCatalog();

    CatalogImportSummary ImportCatalog(
        IReadOnlyList<StoreCatalogItem> items,
        bool preserveExisting);
}
