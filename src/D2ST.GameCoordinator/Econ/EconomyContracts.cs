using D2ST.Core.Economy;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Econ;

public sealed record StoreCatalogComponent(uint ProductId, uint Quantity);

public sealed record StoreCatalogItem(
    uint ProductId,
    uint DefIndex,
    string Name,
    StoreProductType ProductType,
    long PriceCredits,
    string Category,
    string Description,
    uint BuildVersion,
    bool Active,
    IReadOnlyList<StoreCatalogComponent> Components);

public sealed record WalletSnapshot(
    uint AccountId,
    long BalanceCredits,
    long ReservedCredits,
    DateTimeOffset? UpdatedAt)
{
    public long AvailableCredits => Math.Max(0, BalanceCredits - ReservedCredits);

    public static WalletSnapshot Empty(uint accountId) => new(accountId, 0, 0, null);
}

public sealed record WalletTransactionSummary(
    long Id,
    EconomyTransactionKind Kind,
    long AmountCredits,
    long BalanceAfterCredits,
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

/// <summary>
/// Persistence boundary for the local wallet, catalog and econ inventory. The
/// GC project consumes this contract without taking a dependency on EF Core.
/// </summary>
public interface IEconomyStore
{
    IReadOnlyList<StoreCatalogItem> GetCatalog(bool activeOnly = true);

    StoreCatalogItem? FindProduct(uint productIdOrDefIndex, bool activeOnly = true);

    WalletSnapshot GetWallet(uint accountId);

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

    CatalogImportSummary ImportCatalog(
        IReadOnlyList<StoreCatalogItem> items,
        bool preserveExisting);
}
