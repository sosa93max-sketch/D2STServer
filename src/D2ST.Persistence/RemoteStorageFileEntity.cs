using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>One cloud-saved file. Keyed by (account, file name), as Steam does.</summary>
public sealed class RemoteStorageFileEntity
{
    public uint AccountId { get; set; }

    [MaxLength(260)]
    public required string FileName { get; set; }

    public required byte[] Content { get; set; }

    public uint SyncPlatforms { get; set; }

    /// <summary>Bumped on every write so a client can detect a newer save.</summary>
    public int Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
