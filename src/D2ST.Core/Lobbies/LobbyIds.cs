namespace D2ST.Core.Lobbies;

/// <summary>
/// Lobby ids are Steam ids of the chat account type with the lobby instance
/// flag set; the client checks that shape before treating an id as a lobby.
/// </summary>
public static class LobbyIds
{
    private const ulong PublicUniverse = 1UL << 56;
    private const ulong ChatAccountType = 8UL << 52;
    private const ulong LobbyInstanceFlag = 0x40000UL << 32;

    public static ulong FromSequence(uint sequence) =>
        PublicUniverse | ChatAccountType | LobbyInstanceFlag | sequence;

    public static bool IsLobby(ulong steamId) =>
        (steamId & (0xFUL << 52)) == ChatAccountType && (steamId & LobbyInstanceFlag) != 0;
}
