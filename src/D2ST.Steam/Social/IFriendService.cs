namespace D2ST.Steam.Social;

/// <summary>
/// Friend list mutations. Every call that changes a relationship notifies both
/// sides through the event stream so the in-game friend list updates live.
/// </summary>
public interface IFriendService
{
    /// <summary>
    /// Invites another player. Accepting an invitation that already exists in
    /// the other direction befriends the pair instead of creating a second one.
    /// </summary>
    Task<bool> RequestAsync(uint accountId, uint targetAccountId, CancellationToken cancellationToken = default);

    Task<bool> AcceptAsync(uint accountId, uint fromAccountId, CancellationToken cancellationToken = default);

    /// <summary>Removes a friend, or withdraws/declines a pending invitation.</summary>
    Task<bool> RemoveAsync(uint accountId, uint otherAccountId, CancellationToken cancellationToken = default);
}
