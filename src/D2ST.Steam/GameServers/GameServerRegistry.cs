using System.Collections.Concurrent;
using D2ST.Core.GameServers;
using Microsoft.Extensions.Options;

namespace D2ST.Steam.GameServers;

public sealed class GameServerRegistry : IGameServerRegistry
{
    private const ulong GameServerSteamIdBase = (1UL << 56) | (3UL << 52) | (1UL << 32);

    private readonly ConcurrentDictionary<ulong, Registration> _servers = new();
    private readonly TimeProvider _time;
    private readonly IOptions<SteamOptions> _options;
    private int _identities;

    public GameServerRegistry(TimeProvider time, IOptions<SteamOptions> options)
    {
        _time = time;
        _options = options;
    }

    /// <summary>Loopback: every peer here reaches the server on the same host.</summary>
    public uint PublicIp => 2130706433;

    public GameServerRegistration Register(GameServer server, bool anonymous, uint ownerAccountId)
    {
        // A server that has no identity yet gets one from the game-server
        // account type, which is what the client checks before trusting it.
        var steamId = server.SteamId != 0
            ? server.SteamId
            : GameServerSteamIdBase + (uint)Interlocked.Increment(ref _identities);

        var stored = server with { SteamId = steamId, LoggedOn = true };
        _servers[steamId] = new Registration(stored, _time.GetUtcNow(), ownerAccountId);
        return new GameServerRegistration(true, PublicIp, stored.Secure, steamId);
    }

    public bool Update(GameServer server) => Store(server, keepPlayers: true);

    public bool Heartbeat(GameServer server) => Store(server, keepPlayers: true);

    public bool LogOff(ulong steamId) => _servers.TryRemove(steamId, out _);

    public GameServer? Find(ulong steamId) => _servers.TryGetValue(steamId, out var registration)
        ? registration.Server
        : null;

    public GameServer? FindByOwner(uint ownerAccountId) => _servers.Values
        .Where(registration => registration.OwnerAccountId == ownerAccountId)
        .OrderByDescending(registration => registration.LastSeenAt)
        .Select(registration => registration.Server)
        .FirstOrDefault();

    public IReadOnlyList<GameServer> List(uint appId)
    {
        var cutoff = _time.GetUtcNow() - _options.Value.PresenceTimeout;
        return _servers.Values
            .Where(registration => registration.LastSeenAt >= cutoff)
            .Select(registration => registration.Server)
            .Where(server => appId == 0 || server.AppId == appId)
            .ToList();
    }

    public bool SetPlayer(ulong serverSteamId, GameServerPlayer player) => Mutate(serverSteamId, players =>
    {
        players.RemoveAll(existing => existing.SteamId == player.SteamId);
        players.Add(player);
    });

    public bool RemovePlayer(ulong serverSteamId, ulong playerSteamId) => Mutate(
        serverSteamId,
        players => players.RemoveAll(player => player.SteamId == playerSteamId));

    private bool Store(GameServer server, bool keepPlayers)
    {
        if (server.SteamId == 0 || !_servers.TryGetValue(server.SteamId, out var registration))
        {
            return false;
        }

        // The player list is tracked through the connect/disconnect calls, so a
        // state update must not overwrite it with whatever the server sent.
        var players = keepPlayers && server.Players.Count == 0
            ? registration.Server.Players
            : server.Players;

        _servers[server.SteamId] = registration with
        {
            Server = server with { Players = players },
            LastSeenAt = _time.GetUtcNow()
        };
        return true;
    }

    private bool Mutate(ulong serverSteamId, Action<List<GameServerPlayer>> mutate)
    {
        if (!_servers.TryGetValue(serverSteamId, out var registration))
        {
            return false;
        }

        var players = registration.Server.Players.ToList();
        mutate(players);
        _servers[serverSteamId] = registration with { Server = registration.Server with { Players = players } };
        return true;
    }

    private sealed record Registration(GameServer Server, DateTimeOffset LastSeenAt, uint OwnerAccountId);
}
