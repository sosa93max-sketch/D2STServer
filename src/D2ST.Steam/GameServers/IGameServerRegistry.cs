using D2ST.Core.GameServers;

namespace D2ST.Steam.GameServers;

/// <summary>
/// The server browser's backing directory. Registrations are volatile: a
/// server that stops sending heartbeats has, for all practical purposes, gone
/// away, so nothing about it is worth persisting.
/// </summary>
public interface IGameServerRegistry
{
    /// <summary>
    /// Registers a server owned by the calling session's account, which is how
    /// later per-server calls (players, state) find which server is speaking.
    /// </summary>
    GameServerRegistration Register(GameServer server, bool anonymous, uint ownerAccountId);

    bool Update(GameServer server);

    bool Heartbeat(GameServer server);

    bool LogOff(ulong steamId);

    GameServer? Find(ulong steamId);

    GameServer? FindByOwner(uint ownerAccountId);

    IReadOnlyList<GameServer> List(uint appId);

    uint PublicIp { get; }

    bool SetPlayer(ulong serverSteamId, GameServerPlayer player);

    bool RemovePlayer(ulong serverSteamId, ulong playerSteamId);
}
