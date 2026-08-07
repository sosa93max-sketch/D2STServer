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

    /// <summary>
    /// Creates an account with a known password (admin web). False when the
    /// username is taken or the input is invalid.
    /// </summary>
    Task<bool> CreateUserAsync(
        string username,
        string password,
        string? personaName,
        CancellationToken cancellationToken = default);

    /// <summary>Overwrites an account's password. False when it does not exist.</summary>
    Task<bool> SetPasswordAsync(
        uint accountId,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Overwrites an account's persona name. False when it does not exist.</summary>
    Task<bool> SetPersonaAsync(
        uint accountId,
        string personaName,
        CancellationToken cancellationToken = default);
}
