namespace D2ST.Core.Lobbies;

/// <summary>
/// Immutable snapshot of a lobby as the client sees it. Lobbies are volatile
/// (they disappear with the last member), so they are never persisted.
/// </summary>
public sealed record Lobby
{
    public required ulong SteamId { get; init; }

    public required uint AppId { get; init; }

    public required ulong OwnerSteamId { get; init; }

    /// <summary>Steam's ELobbyType: private, friends only, public, invisible.</summary>
    public int LobbyType { get; init; }

    public int MaxMembers { get; init; }

    public bool Joinable { get; init; } = true;

    public IReadOnlyDictionary<string, string> LobbyData { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<LobbyMember> Members { get; init; } = Array.Empty<LobbyMember>();

    public LobbyGameServer GameServer { get; init; } = LobbyGameServer.None;
}
