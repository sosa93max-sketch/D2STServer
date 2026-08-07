namespace D2ST.Core.Auth;

/// <summary>
/// A session ticket handed to the client so it can prove its identity to a
/// game server. The bytes are opaque to the client: only this server ever
/// parses them, which is what lets validation be a plain lookup.
/// </summary>
public sealed record AuthTicket(uint Handle, byte[] Ticket, ulong SteamId, uint AppId, bool GameServer);

/// <param name="BeginAuthSessionResult">EResult of the begin-auth call itself.</param>
/// <param name="AuthSessionResponse">EAuthSessionResponse the caller replays as a callback.</param>
public sealed record TicketValidation(
    int BeginAuthSessionResult,
    int AuthSessionResponse,
    ulong OwnerSteamId,
    bool Success)
{
    public const int ResultOk = 1;
    public const int ResultInvalidParam = 8;
    public const int SessionResponseOk = 0;
    public const int SessionResponseAuthTicketInvalid = 2;
}

/// <summary>Outcome of a game server's ConnectAndAuthenticate.</summary>
public sealed record ConnectAuthResult(
    bool Success,
    ulong SteamId,
    ulong OwnerSteamId,
    int DenyReason,
    string DenyMessage);
