using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>
/// A friend invitation. Answered requests are kept (not deleted) so the pair
/// can be re-invited later without losing the history of who asked whom.
/// </summary>
public sealed class FriendRequestEntity
{
    [Key]
    [MaxLength(32)]
    public required string Id { get; set; }

    public uint FromAccountId { get; set; }

    public uint ToAccountId { get; set; }

    public FriendRequestStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }
}

public enum FriendRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Cancelled = 3
}
