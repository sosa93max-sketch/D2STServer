using D2ST.Core.Storage;

namespace D2ST.Steam.Storage;

/// <summary>Steam Cloud: per-account files the game reads and writes.</summary>
public interface IRemoteStorageService
{
    Task<IReadOnlyList<StorageFile>> ListAsync(uint accountId, CancellationToken cancellationToken = default);

    Task<StorageFile?> FindAsync(uint accountId, string fileName, CancellationToken cancellationToken = default);

    Task<StorageFile?> WriteAsync(
        uint accountId,
        string fileName,
        byte[] content,
        uint? syncPlatforms,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(uint accountId, string fileName, CancellationToken cancellationToken = default);

    Task<StorageQuota> QuotaAsync(uint accountId, CancellationToken cancellationToken = default);
}
