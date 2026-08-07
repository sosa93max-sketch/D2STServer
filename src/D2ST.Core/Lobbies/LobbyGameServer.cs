namespace D2ST.Core.Lobbies;

/// <summary>
/// The server a lobby is playing on. An address of 0.0.0.0:0 means no server
/// has been set: the client only follows a lobby to a server once both the
/// address and the port are filled in.
/// </summary>
public sealed record LobbyGameServer(ulong SteamId, uint Ip, uint Port)
{
    public static readonly LobbyGameServer None = new(0, 0, 0);

    public bool Filled => Ip != 0 && Port != 0;
}
