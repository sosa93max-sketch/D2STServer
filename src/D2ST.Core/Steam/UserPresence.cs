namespace D2ST.Core.Steam;

/// <summary>
/// Volatile presence for one player: what the client last advertised about
/// itself. It is deliberately not persisted — a player who is not connected has
/// no presence, and everything here is re-published on the next logon.
/// </summary>
public sealed class UserPresence
{
    public uint AppId { get; set; }

    public ulong LobbyId { get; set; }

    public ulong GameServerSteamId { get; set; }

    public uint GameServerIp { get; set; }

    public ushort GameServerPort { get; set; }

    public Dictionary<string, string> RichPresence { get; } = new(StringComparer.Ordinal);

    public void Clear()
    {
        AppId = 0;
        LobbyId = 0;
        ClearGameServer();
        RichPresence.Clear();
    }

    public void ClearGameServer()
    {
        GameServerSteamId = 0;
        GameServerIp = 0;
        GameServerPort = 0;
    }
}
