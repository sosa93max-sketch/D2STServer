using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>Catalogue row for a published file; the content is not hosted here.</summary>
public sealed class WorkshopItemEntity
{
    [Key]
    public ulong PublishedFileId { get; set; }

    public uint CreatorAppId { get; set; }

    public uint ConsumerAppId { get; set; }

    public ulong OwnerSteamId { get; set; }

    public int FileType { get; set; }

    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4096)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string Tags { get; set; } = string.Empty;

    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(4096)]
    public string Metadata { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string PreviewUrl { get; set; } = string.Empty;

    public int Visibility { get; set; }

    public bool Banned { get; set; }

    public bool AcceptedForUse { get; set; }

    public uint TimeCreated { get; set; }

    public uint TimeUpdated { get; set; }

    public long FileSize { get; set; }

    public long TotalFilesSize { get; set; }

    public uint VotesUp { get; set; }

    public uint VotesDown { get; set; }

    public float Score { get; set; }
}

public sealed class WorkshopSubscriptionEntity
{
    public uint AccountId { get; set; }

    public ulong PublishedFileId { get; set; }

    public DateTimeOffset SubscribedAtUtc { get; set; }

    public bool DisabledLocally { get; set; }
}
