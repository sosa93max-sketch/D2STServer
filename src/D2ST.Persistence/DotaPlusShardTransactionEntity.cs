using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>Append-only local ledger for Dota Plus shards.</summary>
public sealed class DotaPlusShardTransactionEntity
{
    public long Id { get; set; }

    public uint AccountId { get; set; }

    public uint ChangedByAccountId { get; set; }

    public long Amount { get; set; }

    public long BalanceAfter { get; set; }

    [MaxLength(96)]
    public required string Reference { get; set; }

    [MaxLength(256)]
    public required string Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
