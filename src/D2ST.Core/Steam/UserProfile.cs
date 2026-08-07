using D2ST.Core.Social;

namespace D2ST.Core.Steam;

/// <summary>
/// A player as seen by another player (or by themselves): stored identity plus
/// the live presence resolved at read time. Offline users report no app, lobby,
/// game server or rich presence, because the client renders any of those as
/// "in game".
/// </summary>
public sealed record UserProfile
{
    public required ulong SteamId { get; init; }

    public required uint AccountId { get; init; }

    public required string PersonaName { get; init; }

    public uint AppId { get; init; }

    public ulong LobbyId { get; init; }

    public ulong GameServerSteamId { get; init; }

    public uint GameServerIp { get; init; }

    public ushort GameServerPort { get; init; }

    /// <summary>1 when the player has a live game session, otherwise 0.</summary>
    public int PersonaState { get; init; }

    public FriendRelationship Relationship { get; init; } = FriendRelationship.None;

    public IReadOnlyDictionary<string, string> RichPresence { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool IsFriend => Relationship == FriendRelationship.Friend;
}
