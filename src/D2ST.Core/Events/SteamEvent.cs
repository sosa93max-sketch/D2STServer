using D2ST.Core.Lobbies;
using D2ST.Core.Social;
using D2ST.Core.Steam;

namespace D2ST.Core.Events;

/// <summary>
/// One server-pushed event drained by the client through the event stream. The
/// client dispatches on <see cref="Type"/> and only reads the fields that type
/// carries, so unrelated members stay at their defaults.
/// </summary>
public sealed record SteamEvent
{
    public required string Type { get; init; }

    /// <summary>Player the event is about (not the recipient).</summary>
    public ulong SteamId { get; init; }

    public uint AccountId { get; init; }

    public string PersonaName { get; init; } = string.Empty;

    public uint AppId { get; init; }

    public ulong LobbyId { get; init; }

    public ulong GameServerSteamId { get; init; }

    public uint GameServerIp { get; init; }

    public ushort GameServerPort { get; init; }

    public int PersonaState { get; init; }

    public PersonaChange ChangeFlags { get; init; }

    public FriendRelationship FriendRelationship { get; init; }

    public string RequestId { get; init; } = string.Empty;

    /// <summary>Name of the game an invite refers to, shown in the overlay.</summary>
    public string GameName { get; init; } = string.Empty;

    /// <summary>Whole lobby snapshot carried by every lobby_* event.</summary>
    public Lobby? Lobby { get; init; }

    /// <summary>Chat message, connect string or relayed P2P datagram.</summary>
    public string PayloadBase64 { get; init; } = string.Empty;

    /// <summary>GC message id carried by gc_message.</summary>
    public uint MessageType { get; init; }

    /// <summary>Job the GC message answers, when it answers one.</summary>
    public ulong? TargetJobId { get; init; }

    /// <summary>Whether the GC message body is protobuf (everything this GC sends is).</summary>
    public bool Protobuf { get; init; }

    /// <summary>Sender of a relayed P2P packet.</summary>
    public ulong RemoteSteamId { get; init; }

    public int Channel { get; init; }

    public string Transport { get; init; } = string.Empty;

    public int VirtualPort { get; init; }

    public uint SourceConnectionId { get; init; }

    public uint TargetConnectionId { get; init; }

    /// <summary>Stat written by stats_updated.</summary>
    public string StatName { get; init; } = string.Empty;

    public uint StatValue { get; init; }

    /// <summary>Achievement written by achievement_unlocked.</summary>
    public string AchievementName { get; init; } = string.Empty;

    public bool AchievementEarned { get; init; }

    public uint AchievementProgress { get; init; }

    public uint AchievementMaxProgress { get; init; }

    public IReadOnlyDictionary<string, string> RichPresence { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
