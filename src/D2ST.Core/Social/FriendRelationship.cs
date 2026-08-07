namespace D2ST.Core.Social;

/// <summary>
/// Steamworks EFriendRelationship. The numeric values are part of the client
/// ABI (the shim hands them to the game verbatim), so they are pinned here.
/// </summary>
public enum FriendRelationship
{
    None = 0,
    Blocked = 1,
    RequestRecipient = 2,
    Friend = 3,
    RequestInitiator = 4,
    Ignored = 5,
    IgnoredFriend = 6
}
