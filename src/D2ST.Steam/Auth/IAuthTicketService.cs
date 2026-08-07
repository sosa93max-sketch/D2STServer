using D2ST.Core.Auth;
using D2ST.Core.Steam;

namespace D2ST.Steam.Auth;

/// <summary>
/// Issues and validates the session tickets a client shows to a game server.
/// Tickets are only meaningful inside this deployment, so they are minted and
/// checked here instead of being cryptographically verifiable on their own.
/// </summary>
public interface IAuthTicketService
{
    AuthTicket Create(SteamSession session, uint appId, ulong steamId, bool gameServer);

    /// <summary>Encrypted app ticket: the user data echoed back, unencrypted.</summary>
    byte[] CreateEncryptedAppTicket(SteamSession session, uint appId, byte[] userData);

    TicketValidation Validate(byte[] ticket, ulong steamId, uint appId);

    ConnectAuthResult ConnectAndAuthenticate(byte[] authBlob, ulong steamId, uint appId);

    void EndSession(ulong steamId);

    void Cancel(uint handle);
}
