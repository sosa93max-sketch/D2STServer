using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>Audit trail for local Dota Plus activation changes.</summary>
public sealed class DotaPlusTransactionEntity
{
    public long Id { get; set; }

    public uint AccountId { get; set; }

    public uint ChangedByAccountId { get; set; }

    [MaxLength(32)]
    public required string Action { get; set; }

    public int Days { get; set; }

    [MaxLength(256)]
    public required string Reason { get; set; }

    public DateTimeOffset? ExpiresAtAfter { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
