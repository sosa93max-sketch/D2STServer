using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>One locally-managed Dota Plus entitlement per account.</summary>
public sealed class DotaPlusAccountEntity
{
    [Key]
    public uint AccountId { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public uint PlusFlags { get; set; }

    public ulong SteamAgreementId { get; set; }

    public long Shards { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
