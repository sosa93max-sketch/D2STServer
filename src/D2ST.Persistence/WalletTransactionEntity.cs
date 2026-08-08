using System.ComponentModel.DataAnnotations;
using D2ST.Core.Economy;

namespace D2ST.Persistence;

/// <summary>
/// Immutable wallet ledger row. Reference is unique so rewards and purchases
/// remain idempotent even if a caller retries the same operation.
/// </summary>
public sealed class WalletTransactionEntity
{
    [Key]
    public long Id { get; set; }

    public uint AccountId { get; set; }

    public EconomyTransactionKind Kind { get; set; }

    public long AmountCredits { get; set; }

    public long BalanceAfterCredits { get; set; }

    [MaxLength(160)]
    public required string Reference { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
