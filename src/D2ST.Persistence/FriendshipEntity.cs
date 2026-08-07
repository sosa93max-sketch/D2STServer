namespace D2ST.Persistence;

/// <summary>
/// One direction of a friendship. Both directions are stored so a player's
/// friend list is a single indexed lookup, and so an asymmetric state (a
/// pending request) is representable without a second table shape.
/// </summary>
public sealed class FriendshipEntity
{
    public uint AccountId { get; set; }

    public uint FriendAccountId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
