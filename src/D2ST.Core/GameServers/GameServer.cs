namespace D2ST.Core.GameServers;

/// <summary>
/// A registered game server. Everything here is what the server itself
/// advertises; the directory only adds its identity and liveness.
/// </summary>
public sealed record GameServer
{
    public required ulong SteamId { get; init; }

    public uint AppId { get; init; }

    public uint Ip { get; init; }

    public int Port { get; init; }

    public int QueryPort { get; init; }

    public uint Flags { get; init; }

    public byte Secure { get; init; }

    public string VersionString { get; init; } = string.Empty;

    public string Product { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ModDir { get; init; } = string.Empty;

    public bool Dedicated { get; init; }

    public int MaxPlayers { get; init; }

    public int BotPlayers { get; init; }

    public string ServerName { get; init; } = string.Empty;

    public string MapName { get; init; } = string.Empty;

    public bool PasswordProtected { get; init; }

    public uint SpectatorPort { get; init; }

    public string SpectatorServerName { get; init; } = string.Empty;

    public string GameTags { get; init; } = string.Empty;

    public string GameData { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public bool LoggedOn { get; init; }

    public bool AdvertiseActive { get; init; }

    public IReadOnlyDictionary<string, string> KeyValues { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<GameServerPlayer> Players { get; init; } = Array.Empty<GameServerPlayer>();
}

public sealed record GameServerPlayer(ulong SteamId, string Name, int Score, float TimePlayedSeconds);

/// <summary>Answer to a register/logon: the identity the server should use.</summary>
public sealed record GameServerRegistration(bool Success, uint PublicIp, byte Secure, ulong SteamId);
