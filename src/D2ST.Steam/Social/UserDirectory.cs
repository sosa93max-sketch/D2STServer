using System.Security.Cryptography;
using D2ST.Core.Accounts;
using D2ST.Core.Social;
using D2ST.Core.Steam;
using D2ST.Persistence;
using D2ST.Steam.Presence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Steam.Social;

public sealed class UserDirectory : IUserDirectory
{
    // 1x1 transparent PNG: the client only needs a decodable image to fall back
    // on, and it drops it from its cache as soon as the response says default.
    private static readonly byte[] DefaultAvatar = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9WnN0E4AAAAASUVORK5CYII=");

    private readonly D2stDbContext _db;
    private readonly FriendGraph _graph;
    private readonly ISessionStore _sessions;
    private readonly IPresenceTracker _presence;

    public UserDirectory(
        D2stDbContext db,
        FriendGraph graph,
        ISessionStore sessions,
        IPresenceTracker presence)
    {
        _db = db;
        _graph = graph;
        _sessions = sessions;
        _presence = presence;
    }

    public async Task<UserProfile?> FindAsync(
        uint viewerAccountId,
        uint accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.AccountId == accountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        return ToProfile(account, await _graph.RelationshipAsync(viewerAccountId, accountId, cancellationToken));
    }

    public async Task<IReadOnlyList<UserProfile>> ListFriendsAsync(
        uint viewerAccountId,
        CancellationToken cancellationToken = default)
    {
        var friendIds = await _graph.FriendIdsAsync(viewerAccountId, cancellationToken);
        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(entity => friendIds.Contains(entity.AccountId))
            .ToListAsync(cancellationToken);

        return accounts
            .Select(account => ToProfile(account, FriendRelationship.Friend))
            .OrderBy(profile => profile.PersonaName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<UserProfile>> ListAllAsync(
        uint viewerAccountId,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _db.Accounts.AsNoTracking().ToListAsync(cancellationToken);
        var profiles = new List<UserProfile>(accounts.Count);
        foreach (var account in accounts)
        {
            var relationship = await _graph.RelationshipAsync(viewerAccountId, account.AccountId, cancellationToken);
            profiles.Add(ToProfile(account, relationship));
        }

        return profiles
            .OrderBy(profile => profile.PersonaName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<uint> ResolveAccountIdAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return 0;
        }

        identifier = identifier.Trim();
        if (ulong.TryParse(identifier, out var numeric) && numeric != 0)
        {
            var accountId = numeric > SteamAccount.SteamIdBase
                ? SteamAccount.AccountIdFromSteamId(numeric)
                : (uint)numeric;

            return await _db.Accounts.AnyAsync(account => account.AccountId == accountId, cancellationToken)
                ? accountId
                : 0;
        }

        return await _db.Accounts
            .Where(account => account.Username == identifier || account.PersonaName == identifier)
            .Select(account => account.AccountId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserProfile?> SetPersonaNameAsync(
        uint accountId,
        string personaName,
        CancellationToken cancellationToken = default)
    {
        var account = await _db.Accounts.SingleOrDefaultAsync(
            entity => entity.AccountId == accountId,
            cancellationToken);

        if (account is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(personaName) && account.PersonaName != personaName)
        {
            account.PersonaName = personaName;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ToProfile(account, FriendRelationship.Friend);
    }

    public async Task<AvatarContent> GetAvatarAsync(uint accountId, CancellationToken cancellationToken = default)
    {
        var avatar = await _db.Accounts
            .AsNoTracking()
            .Where(entity => entity.AccountId == accountId)
            .Select(entity => entity.Avatar)
            .FirstOrDefaultAsync(cancellationToken);

        var steamId = SteamAccount.SteamIdFromAccountId(accountId);
        return avatar is { Length: > 0 }
            ? Content(steamId, avatar, isDefault: false)
            : Content(steamId, DefaultAvatar, isDefault: true);
    }

    public async Task<bool> SetAvatarAsync(uint accountId, byte[] avatar, CancellationToken cancellationToken = default)
    {
        if (avatar.Length == 0)
        {
            return false;
        }

        var account = await _db.Accounts.SingleOrDefaultAsync(
            entity => entity.AccountId == accountId,
            cancellationToken);

        if (account is null)
        {
            return false;
        }

        account.Avatar = avatar;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private UserProfile ToProfile(AccountEntity account, FriendRelationship relationship)
    {
        var online = _sessions.IsOnline(account.AccountId);
        var presence = _presence.Get(account.AccountId);
        var steamId = SteamAccount.SteamIdFromAccountId(account.AccountId);

        // Everything below the identity is presence: an offline player must
        // report no app, lobby or server, because the client reads any of those
        // as "currently playing".
        if (!online)
        {
            return new UserProfile
            {
                SteamId = steamId,
                AccountId = account.AccountId,
                PersonaName = account.PersonaName ?? account.Username,
                PersonaState = 0,
                Relationship = relationship
            };
        }

        lock (presence)
        {
            return new UserProfile
            {
                SteamId = steamId,
                AccountId = account.AccountId,
                PersonaName = account.PersonaName ?? account.Username,
                AppId = presence.AppId,
                LobbyId = presence.LobbyId,
                GameServerSteamId = presence.GameServerSteamId,
                GameServerIp = presence.GameServerIp,
                GameServerPort = presence.GameServerPort,
                PersonaState = 1,
                Relationship = relationship,
                RichPresence = new Dictionary<string, string>(presence.RichPresence, StringComparer.Ordinal)
            };
        }
    }

    private static AvatarContent Content(ulong steamId, byte[] content, bool isDefault) => new(
        steamId,
        content,
        isDefault,
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
}
