namespace D2ST.Core.Storage;

/// <summary>One cloud-saved file, owned by the account that uploaded it.</summary>
public sealed record StorageFile(
    string FileName,
    byte[] Content,
    uint SyncPlatforms,
    int Version,
    DateTimeOffset UpdatedAt);

public sealed record StorageQuota(ulong TotalBytes, ulong AvailableBytes);
