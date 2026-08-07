using D2ST.Core.Storage;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Steam.Storage;

public sealed class RemoteStorageService : IRemoteStorageService
{
    /// <summary>Steam reports a quota per app; 1 GiB is well past what Dota uses.</summary>
    public const ulong QuotaBytes = 1024UL * 1024 * 1024;

    private readonly D2stDbContext _db;
    private readonly TimeProvider _time;

    public RemoteStorageService(D2stDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IReadOnlyList<StorageFile>> ListAsync(uint accountId, CancellationToken cancellationToken = default) =>
        (await _db.RemoteStorageFiles
            .Where(file => file.AccountId == accountId)
            .ToListAsync(cancellationToken))
        .Select(ToFile)
        .ToList();

    public async Task<StorageFile?> FindAsync(uint accountId, string fileName, CancellationToken cancellationToken = default)
    {
        var stored = await _db.RemoteStorageFiles.FindAsync([accountId, fileName], cancellationToken);
        return stored is null ? null : ToFile(stored);
    }

    public async Task<StorageFile?> WriteAsync(
        uint accountId,
        string fileName,
        byte[] content,
        uint? syncPlatforms,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var stored = await _db.RemoteStorageFiles.FindAsync([accountId, fileName], cancellationToken);
        if (stored is null)
        {
            stored = new RemoteStorageFileEntity
            {
                AccountId = accountId,
                FileName = fileName,
                Content = content,
                SyncPlatforms = syncPlatforms ?? 0
            };

            _db.RemoteStorageFiles.Add(stored);
        }
        else
        {
            stored.Content = content;
            // A write that does not mention the platforms keeps the ones the
            // file was created with.
            stored.SyncPlatforms = syncPlatforms ?? stored.SyncPlatforms;
        }

        stored.Version++;
        stored.UpdatedAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
        return ToFile(stored);
    }

    public async Task<bool> DeleteAsync(uint accountId, string fileName, CancellationToken cancellationToken = default)
    {
        var stored = await _db.RemoteStorageFiles.FindAsync([accountId, fileName], cancellationToken);
        if (stored is null)
        {
            return false;
        }

        _db.RemoteStorageFiles.Remove(stored);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<StorageQuota> QuotaAsync(uint accountId, CancellationToken cancellationToken = default)
    {
        var used = (ulong)await _db.RemoteStorageFiles
            .Where(file => file.AccountId == accountId)
            .SumAsync(file => (long)file.Content.Length, cancellationToken);

        return new StorageQuota(QuotaBytes, used >= QuotaBytes ? 0 : QuotaBytes - used);
    }

    private static StorageFile ToFile(RemoteStorageFileEntity entity) => new(
        entity.FileName,
        entity.Content,
        entity.SyncPlatforms,
        entity.Version,
        entity.UpdatedAt);
}
