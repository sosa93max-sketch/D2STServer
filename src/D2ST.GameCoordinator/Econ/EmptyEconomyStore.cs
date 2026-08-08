using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Econ;

/// <summary>
/// Keeps the reusable GC assembly usable without the API host's persistence
/// implementation. The API replaces this registration with its SQLite store.
/// </summary>
internal sealed class EmptyEconomyStore : IEconomyStore
{
    public IReadOnlyList<StoreCatalogItem> GetCatalog(bool activeOnly = true) => [];

    public StoreCatalogItem? FindProduct(uint productIdOrDefIndex, bool activeOnly = true) => null;

    public WalletSnapshot GetWallet(uint accountId) => WalletSnapshot.Empty(accountId);

    public IReadOnlyList<WalletTransactionSummary> GetTransactions(uint accountId, int limit = 50) => [];

    public IReadOnlyList<CSOEconItem> GetItems(uint accountId) => [];

    public CSOEconItem GrantItem(uint accountId, uint defIndex, uint quantity) =>
        new()
        {
            Id = EconItemIdentity.ItemId(accountId, defIndex),
            OriginalId = EconItemIdentity.ItemId(accountId, defIndex),
            AccountId = accountId,
            DefIndex = defIndex,
            Quantity = quantity == 0 ? 1 : quantity,
            Level = 1,
            Quality = 4,
            Origin = 2,
            Inventory = 1
        };

    public bool SaveItem(uint accountId, CSOEconItem item) => false;

    public StoreOperationResult BeginPurchase(uint accountId, IReadOnlyList<StorePurchaseLine> lines) =>
        StoreOperationResult.Failed("economy_unavailable", "La economía persistente no está disponible.");

    public StoreOperationResult FinalizePurchase(uint accountId, ulong transactionId) =>
        StoreOperationResult.Failed("economy_unavailable", "La economía persistente no está disponible.");

    public StoreOperationResult CancelPurchase(uint accountId, ulong transactionId) =>
        StoreOperationResult.Failed("economy_unavailable", "La economía persistente no está disponible.");

    public int CancelPendingPurchases(uint accountId) => 0;

    public StoreOperationResult Purchase(uint accountId, IReadOnlyList<StorePurchaseLine> lines) =>
        StoreOperationResult.Failed("economy_unavailable", "La economía persistente no está disponible.");

    public bool UpsertCatalogItem(StoreCatalogItem item) => false;
}
