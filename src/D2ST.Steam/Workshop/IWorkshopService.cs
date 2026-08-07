using D2ST.Core.Workshop;

namespace D2ST.Steam.Workshop;

public interface IWorkshopService
{
    Task<IReadOnlyList<WorkshopSubscription>> SubscriptionsAsync(uint accountId, CancellationToken cancellationToken = default);

    Task<WorkshopItem?> FindAsync(ulong publishedFileId, CancellationToken cancellationToken = default);

    Task<WorkshopItem> PutAsync(WorkshopItem item, CancellationToken cancellationToken = default);

    Task<WorkshopSubscription?> SubscribeAsync(uint accountId, ulong publishedFileId, CancellationToken cancellationToken = default);

    Task<bool> UnsubscribeAsync(uint accountId, ulong publishedFileId, CancellationToken cancellationToken = default);
}
