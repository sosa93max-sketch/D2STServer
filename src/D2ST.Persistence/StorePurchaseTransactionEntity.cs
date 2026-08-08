using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

public enum StorePurchaseStatus
{
    Pending = 0,
    Finalized = 1,
    Cancelled = 2
}

/// <summary>
/// Pending/finalized local store checkout. Lines and grants are kept as JSON so
/// a catalog edit cannot change a purchase after its init response.
/// </summary>
public sealed class StorePurchaseTransactionEntity
{
    [Key]
    public long Id { get; set; }

    public uint AccountId { get; set; }

    public long TotalCredits { get; set; }

    public StorePurchaseStatus Status { get; set; }

    [Required]
    public string LinesJson { get; set; } = "[]";

    [Required]
    public string GrantsJson { get; set; } = "[]";

    [Required]
    public string ItemIdsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
