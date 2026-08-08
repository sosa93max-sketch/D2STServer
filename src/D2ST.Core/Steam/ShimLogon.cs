namespace D2ST.Core.Steam;

/// <summary>
/// Identity the injected shim presents at logon. Only the process knows its own
/// role and instance, and the game machine (not the server) picks the Steam id
/// and persona name.
/// </summary>
public sealed record ShimLogon(
    ulong SteamId,
    uint AccountId,
    string? PersonaName,
    uint AppId,
    string? ClientInstanceId,
    string? ProcessRole,
    bool UseActiveWebUser = false,
    string? RemoteIp = null);
