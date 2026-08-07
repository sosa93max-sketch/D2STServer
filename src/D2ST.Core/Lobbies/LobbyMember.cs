namespace D2ST.Core.Lobbies;

/// <summary>One player in a lobby, with the per-member data they published.</summary>
public sealed record LobbyMember(
    ulong SteamId,
    uint AccountId,
    IReadOnlyDictionary<string, string> Data);
