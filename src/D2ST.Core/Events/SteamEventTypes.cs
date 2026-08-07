namespace D2ST.Core.Events;

/// <summary>
/// Event type discriminators. The strings are the client's dispatch keys, so
/// they must match the shim's event pump verbatim.
/// </summary>
public static class SteamEventTypes
{
    public const string PersonaStateChanged = "persona_state_changed";
    public const string FriendPresenceChanged = "friend_presence_changed";
    public const string FriendAdded = "friend_added";
    public const string FriendRemoved = "friend_removed";
    public const string FriendRequestReceived = "friend_request_received";
    public const string FriendRequestSent = "friend_request_sent";
    public const string LobbyCreated = "lobby_created";
    public const string LobbyUpdated = "lobby_updated";
    public const string LobbyMemberUpdated = "lobby_member_updated";
    public const string LobbyJoined = "lobby_joined";
    public const string LobbyLeft = "lobby_left";
    public const string LobbyRemoved = "lobby_removed";
    public const string LobbyChat = "lobby_chat";
    public const string LobbyGameCreated = "lobby_game_created";
    public const string LobbyInvite = "lobby_invite";
    public const string GameInvite = "game_invite";
    public const string P2PPacket = "p2p_packet";

    /// <summary>A GC message the server sends to a client that did not ask for it.</summary>
    public const string GcMessage = "gc_message";

    public const string StatsUpdated = "stats_updated";
    public const string AchievementUnlocked = "achievement_unlocked";
}
