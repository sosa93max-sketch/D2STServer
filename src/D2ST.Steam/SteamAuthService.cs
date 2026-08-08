using System.Security.Cryptography;
using D2ST.Core.Accounts;
using D2ST.Core.Steam;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Steam;

/// <summary>
/// Password auth backed by the accounts table. Passwords are stored as salted
/// PBKDF2 hashes (never plaintext). Unknown usernames are registered on first
/// login, which is the usual flow for a private/LAN emulator.
/// </summary>
public sealed class SteamAuthService : ISteamAuthService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    private readonly D2stDbContext _db;
    private readonly ISessionStore _sessions;

    public SteamAuthService(D2stDbContext db, ISessionStore sessions)
    {
        _db = db;
        _sessions = sessions;
    }

    public async Task<SteamSession?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .SingleOrDefaultAsync(entity => entity.Username == username, cancellationToken);

        if (account is null)
        {
            account = await RegisterAsync(username, password, cancellationToken);
        }
        else if (!Verify(password, account.PasswordSalt, account.PasswordHash))
        {
            return null;
        }

        return IssueSession(account, new ShimLogon(0, account.AccountId, account.PersonaName ?? account.Username, 0, null, null));
    }

    public async Task<SteamSession> CreateShimSessionAsync(
        ShimLogon logon,
        CancellationToken cancellationToken = default)
    {
        var activeWebSession = logon.UseActiveWebUser
            ? _sessions.FindActiveWebSession(logon.RemoteIp)
            : null;

        // The shim sends its machine-level fallback id on every handshake.
        // When the operator enabled UseActiveWebUser, that id is only a
        // fallback: the password-authenticated web session on the same
        // machine is the account the client should actually use.
        var effectiveLogon = activeWebSession is null
            ? logon
            : logon with
            {
                AccountId = activeWebSession.Account.AccountId,
                SteamId = activeWebSession.Account.SteamId,
                PersonaName = activeWebSession.PersonaName ?? logon.PersonaName
            };

        var resolvedAccountId = effectiveLogon.AccountId != 0
            ? effectiveLogon.AccountId
            : effectiveLogon.SteamId != 0
                ? SteamAccount.AccountIdFromSteamId(effectiveLogon.SteamId)
                : 0;

        var account = resolvedAccountId != 0
            ? await _db.Accounts.SingleOrDefaultAsync(entity => entity.AccountId == resolvedAccountId, cancellationToken)
            : null;

        account ??= await RegisterAsync(
            await AvailableUsernameAsync(resolvedAccountId, effectiveLogon.PersonaName, cancellationToken),
            RandomPassword(),
            cancellationToken,
            resolvedAccountId);

        // The persona is chosen on the game machine, so logon is the moment the
        // server learns the name other players will see.
        if (!string.IsNullOrWhiteSpace(effectiveLogon.PersonaName) && account.PersonaName != effectiveLogon.PersonaName)
        {
            account.PersonaName = effectiveLogon.PersonaName;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return IssueSession(account, effectiveLogon);
    }

    private SteamSession IssueSession(AccountEntity account, ShimLogon logon)
    {
        var personaName = string.IsNullOrWhiteSpace(logon.PersonaName)
            ? account.PersonaName ?? account.Username
            : logon.PersonaName;

        var session = new SteamSession
        {
            Account = new SteamAccount(SteamAccount.SteamIdFromAccountId(account.AccountId), account.Username),
            Token = GenerateToken(),
            RefreshToken = GenerateToken(),
            IssuedAt = DateTimeOffset.UtcNow,
            AppId = logon.AppId,
            PersonaName = personaName,
            ClientInstanceId = logon.ClientInstanceId ?? string.Empty,
            ProcessRole = ProcessRoles.Normalize(logon.ProcessRole)
        };

        _sessions.Add(session);
        return session;
    }

    private async Task<AccountEntity> RegisterAsync(
        string username,
        string password,
        CancellationToken cancellationToken,
        uint accountId = 0)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var account = new AccountEntity
        {
            AccountId = accountId != 0 ? accountId : await NextAccountIdAsync(cancellationToken),
            Username = username,
            PasswordSalt = salt,
            PasswordHash = Hash(password, salt),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);
        return account;
    }

    private async Task<uint> NextAccountIdAsync(CancellationToken cancellationToken)
    {
        var hasAny = await _db.Accounts.AnyAsync(cancellationToken);
        if (!hasAny)
        {
            return 1;
        }

        var max = await _db.Accounts.MaxAsync(account => account.AccountId, cancellationToken);
        return max + 1;
    }

    private async Task<string> AvailableUsernameAsync(
        uint accountId,
        string? personaName,
        CancellationToken cancellationToken)
    {
        var fallback = $"account{accountId}";
        if (string.IsNullOrWhiteSpace(personaName))
        {
            return fallback;
        }

        var taken = await _db.Accounts.AnyAsync(entity => entity.Username == personaName, cancellationToken);
        return taken ? fallback : personaName;
    }

    private static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

    private static bool Verify(string password, byte[] salt, byte[] expected) =>
        CryptographicOperations.FixedTimeEquals(Hash(password, salt), expected);

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Shim accounts have no password: store an unguessable one so the row can
    /// never be claimed through the password endpoint.
    /// </summary>
    private static string RandomPassword() => GenerateToken();

    public async Task<bool> CreateUserAsync(
        string username,
        string password,
        string? personaName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var exists = await _db.Accounts
            .AnyAsync(entity => entity.Username == username, cancellationToken);
        if (exists)
        {
            return false;
        }

        var account = await RegisterAsync(username, password, cancellationToken);
        if (!string.IsNullOrWhiteSpace(personaName) && account.PersonaName != personaName)
        {
            account.PersonaName = personaName;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> SetPasswordAsync(
        uint accountId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .SingleOrDefaultAsync(entity => entity.AccountId == accountId, cancellationToken);
        if (account is null || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        account.PasswordSalt = RandomNumberGenerator.GetBytes(SaltSize);
        account.PasswordHash = Hash(password, account.PasswordSalt);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetPersonaAsync(
        uint accountId,
        string personaName,
        CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .SingleOrDefaultAsync(entity => entity.AccountId == accountId, cancellationToken);
        if (account is null || string.IsNullOrWhiteSpace(personaName))
        {
            return false;
        }

        account.PersonaName = personaName;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
