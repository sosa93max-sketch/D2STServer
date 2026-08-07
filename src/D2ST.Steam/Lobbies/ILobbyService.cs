using D2ST.Core.Lobbies;
using D2ST.Core.Steam;

namespace D2ST.Steam.Lobbies;

/// <summary>
/// Matchmaking lobbies. Every mutation returns the resulting snapshot (or null
/// when the caller may not perform it) and pushes the matching lobby_* event to
/// the members, which is how the client learns about changes it did not make.
/// </summary>
public interface ILobbyService
{
    Lobby Create(SteamSession session, uint appId, int lobbyType, int maxMembers, IReadOnlyDictionary<string, string>? lobbyData);

    Lobby? Find(ulong lobbyId);

    IReadOnlyList<Lobby> Query(LobbyQuery query);

    Lobby? Join(SteamSession session, ulong lobbyId);

    bool Leave(SteamSession session, ulong lobbyId);

    /// <summary>Owner-only. A null <paramref name="value"/> deletes the key.</summary>
    bool SetLobbyData(SteamSession session, ulong lobbyId, string key, string? value);

    /// <summary>Any member, on its own row.</summary>
    bool SetMemberData(SteamSession session, ulong lobbyId, string key, string? value);

    bool SetGameServer(SteamSession session, ulong lobbyId, ulong gameServerSteamId, uint ip, uint port);

    bool UpdateSettings(SteamSession session, ulong lobbyId, LobbySettingsUpdate update);

    bool SendChat(SteamSession session, ulong lobbyId, string messageBase64);

    bool Invite(SteamSession session, ulong lobbyId, ulong inviteeSteamId);

    /// <summary>Removes the account from every lobby it is in (logoff/timeout).</summary>
    void LeaveAll(uint accountId);
}

/// <summary>Owner-only settings; unset members are left unchanged.</summary>
public sealed record LobbySettingsUpdate(bool? Joinable, int? LobbyType, ulong? OwnerSteamId, int? MaxMembers);
