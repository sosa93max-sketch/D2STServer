using D2ST.Core.Steam;

namespace D2ST.Steam;

public interface ISteamAuthService
{
    /// <summary>
    /// Authenticates a user, registering the account on first login. Returns null
    /// when the username exists but the password does not match.
    /// </summary>
    Task<SteamSession?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Passwordless logon used by the injected Steamworks shim, which identifies
    /// itself with the Steam id / persona name configured on the game machine.
    /// The account is created on first contact.
    /// </summary>
    Task<SteamSession> CreateShimSessionAsync(ShimLogon logon, CancellationToken cancellationToken = default);
}
