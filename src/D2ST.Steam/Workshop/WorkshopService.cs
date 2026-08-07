using D2ST.Core.Workshop;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Steam.Workshop;

public sealed class WorkshopService : IWorkshopService
{
    private readonly D2stDbContext _db;
    private readonly TimeProvider _time;

    public WorkshopService(D2stDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IReadOnlyList<WorkshopSubscription>> SubscriptionsAsync(
        uint accountId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await _db.WorkshopSubscriptions
            .Where(subscription => subscription.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var ids = subscriptions.Select(subscription => subscription.PublishedFileId).ToList();
        var items = await _db.WorkshopItems
            .Where(item => ids.Contains(item.PublishedFileId))
            .ToDictionaryAsync(item => item.PublishedFileId, cancellationToken);

        return subscriptions
            .Select(subscription => new WorkshopSubscription(
                subscription.PublishedFileId,
                subscription.SubscribedAtUtc,
                subscription.DisabledLocally,
                items.TryGetValue(subscription.PublishedFileId, out var item) ? ToItem(item) : null))
            .ToList();
    }

    public async Task<WorkshopItem?> FindAsync(ulong publishedFileId, CancellationToken cancellationToken = default)
    {
        var stored = await _db.WorkshopItems.FindAsync([publishedFileId], cancellationToken);
        return stored is null ? null : ToItem(stored);
    }

    public async Task<WorkshopItem> PutAsync(WorkshopItem item, CancellationToken cancellationToken = default)
    {
        var stored = await _db.WorkshopItems.FindAsync([item.PublishedFileId], cancellationToken);
        if (stored is null)
        {
            stored = new WorkshopItemEntity { PublishedFileId = item.PublishedFileId };
            _db.WorkshopItems.Add(stored);
        }

        stored.CreatorAppId = item.CreatorAppId;
        stored.ConsumerAppId = item.ConsumerAppId;
        stored.OwnerSteamId = item.OwnerSteamId;
        stored.FileType = item.FileType;
        stored.Title = item.Title;
        stored.Description = item.Description;
        stored.Tags = item.Tags;
        stored.FileName = item.FileName;
        stored.Metadata = item.Metadata;
        stored.PreviewUrl = item.PreviewUrl;
        stored.Visibility = item.Visibility;
        stored.Banned = item.Banned;
        stored.AcceptedForUse = item.AcceptedForUse;
        stored.TimeCreated = item.TimeCreated != 0 ? item.TimeCreated : Now();
        stored.TimeUpdated = Now();
        stored.FileSize = item.FileSize;
        stored.TotalFilesSize = item.TotalFilesSize;
        stored.VotesUp = item.VotesUp;
        stored.VotesDown = item.VotesDown;
        stored.Score = item.Score;

        await _db.SaveChangesAsync(cancellationToken);
        return ToItem(stored);
    }

    public async Task<WorkshopSubscription?> SubscribeAsync(
        uint accountId,
        ulong publishedFileId,
        CancellationToken cancellationToken = default)
    {
        var stored = await _db.WorkshopSubscriptions.FindAsync([accountId, publishedFileId], cancellationToken);
        if (stored is null)
        {
            stored = new WorkshopSubscriptionEntity
            {
                AccountId = accountId,
                PublishedFileId = publishedFileId,
                SubscribedAtUtc = _time.GetUtcNow()
            };

            _db.WorkshopSubscriptions.Add(stored);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new WorkshopSubscription(
            publishedFileId,
            stored.SubscribedAtUtc,
            stored.DisabledLocally,
            await FindAsync(publishedFileId, cancellationToken));
    }

    public async Task<bool> UnsubscribeAsync(uint accountId, ulong publishedFileId, CancellationToken cancellationToken = default)
    {
        var stored = await _db.WorkshopSubscriptions.FindAsync([accountId, publishedFileId], cancellationToken);
        if (stored is null)
        {
            return false;
        }

        _db.WorkshopSubscriptions.Remove(stored);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private uint Now() => (uint)_time.GetUtcNow().ToUnixTimeSeconds();

    private static WorkshopItem ToItem(WorkshopItemEntity entity) => new()
    {
        PublishedFileId = entity.PublishedFileId,
        CreatorAppId = entity.CreatorAppId,
        ConsumerAppId = entity.ConsumerAppId,
        OwnerSteamId = entity.OwnerSteamId,
        FileType = entity.FileType,
        Title = entity.Title,
        Description = entity.Description,
        Tags = entity.Tags,
        FileName = entity.FileName,
        Metadata = entity.Metadata,
        PreviewUrl = entity.PreviewUrl,
        Visibility = entity.Visibility,
        Banned = entity.Banned,
        AcceptedForUse = entity.AcceptedForUse,
        TimeCreated = entity.TimeCreated,
        TimeUpdated = entity.TimeUpdated,
        FileSize = entity.FileSize,
        TotalFilesSize = entity.TotalFilesSize,
        VotesUp = entity.VotesUp,
        VotesDown = entity.VotesDown,
        Score = entity.Score
    };
}
