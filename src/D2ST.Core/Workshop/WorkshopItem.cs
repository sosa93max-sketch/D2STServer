namespace D2ST.Core.Workshop;

/// <summary>
/// A published file. The server is only a catalogue: it stores the metadata
/// the client publishes and never hosts the content itself.
/// </summary>
public sealed record WorkshopItem
{
    public required ulong PublishedFileId { get; init; }

    public uint CreatorAppId { get; init; }

    public uint ConsumerAppId { get; init; }

    public ulong OwnerSteamId { get; init; }

    public int FileType { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Tags { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string Metadata { get; init; } = string.Empty;

    public string PreviewUrl { get; init; } = string.Empty;

    public int Visibility { get; init; }

    public bool Banned { get; init; }

    public bool AcceptedForUse { get; init; }

    public uint TimeCreated { get; init; }

    public uint TimeUpdated { get; init; }

    public long FileSize { get; init; }

    public long TotalFilesSize { get; init; }

    public uint VotesUp { get; init; }

    public uint VotesDown { get; init; }

    public float Score { get; init; }
}

public sealed record WorkshopSubscription(
    ulong PublishedFileId,
    DateTimeOffset SubscribedAtUtc,
    bool DisabledLocally,
    WorkshopItem? Item);
