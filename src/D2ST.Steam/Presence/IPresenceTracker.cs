using D2ST.Core.Steam;

namespace D2ST.Steam.Presence;

/// <summary>
/// Live, non-persisted presence: what each connected client advertises about
/// itself (rich presence keys, current lobby, the game server it is playing on).
/// </summary>
public interface IPresenceTracker
{
    UserPresence Get(uint accountId);

    void SetRichPresence(uint accountId, string key, string? value);

    void SetGameServer(uint accountId, ulong gameServerSteamId, uint ip, ushort port);

    void SetAppId(uint accountId, uint appId);

    /// <summary>Lobby the account is currently in; 0 once it leaves.</summary>
    void SetLobby(uint accountId, ulong lobbyId);

    /// <summary>Drops everything the account advertised (logoff or timeout).</summary>
    void Clear(uint accountId);
}
