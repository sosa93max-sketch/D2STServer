using System.Collections.Concurrent;
using D2ST.Core.Steam;

namespace D2ST.Steam.Presence;

public sealed class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<uint, UserPresence> _presence = new();

    public UserPresence Get(uint accountId) => _presence.GetOrAdd(accountId, static _ => new UserPresence());

    public void SetRichPresence(uint accountId, string key, string? value)
    {
        var presence = Get(accountId);
        lock (presence)
        {
            if (string.IsNullOrEmpty(value))
            {
                presence.RichPresence.Remove(key);
            }
            else
            {
                presence.RichPresence[key] = value;
            }
        }
    }

    public void SetGameServer(uint accountId, ulong gameServerSteamId, uint ip, ushort port)
    {
        var presence = Get(accountId);
        lock (presence)
        {
            // The client clears its advertised server by sending a zeroed
            // address; keeping the steam id in that case would leave friends
            // trying to connect to a server that is no longer being played on.
            if (ip == 0 || port == 0)
            {
                presence.ClearGameServer();
                return;
            }

            presence.GameServerSteamId = gameServerSteamId;
            presence.GameServerIp = ip;
            presence.GameServerPort = port;
        }
    }

    public void SetAppId(uint accountId, uint appId)
    {
        var presence = Get(accountId);
        lock (presence)
        {
            presence.AppId = appId;
        }
    }

    public void SetLobby(uint accountId, ulong lobbyId)
    {
        var presence = Get(accountId);
        lock (presence)
        {
            presence.LobbyId = lobbyId;
        }
    }

    public void Clear(uint accountId)
    {
        var presence = Get(accountId);
        lock (presence)
        {
            presence.Clear();
        }
    }
}
