using D2ST.Core.Steam;

namespace D2ST.Steam.Social;

/// <summary>
/// Read model over the account table: resolves stored identity plus live
/// presence and the viewer's relationship into the <see cref="UserProfile"/>
/// the client consumes.
/// </summary>
public interface IUserDirectory
{
    Task<UserProfile?> FindAsync(uint viewerAccountId, uint accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> ListFriendsAsync(uint viewerAccountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> ListAllAsync(uint viewerAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a Steam id, account id or persona/user name to an account id,
    /// returning 0 when nothing matches. The client's "add friend" flow lets a
    /// player type any of the three.
    /// </summary>
    Task<uint> ResolveAccountIdAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>Renames the player.</summary>
    Task<UserProfile?> SetPersonaNameAsync(uint accountId, string personaName, CancellationToken cancellationToken = default);

    Task<AvatarContent> GetAvatarAsync(uint accountId, CancellationToken cancellationToken = default);

    Task<bool> SetAvatarAsync(uint accountId, byte[] avatar, CancellationToken cancellationToken = default);
}

/// <param name="IsDefault">
/// True when the player never uploaded an avatar and the placeholder is being
/// served, which the client uses to drop its cached copy.
/// </param>
public sealed record AvatarContent(ulong SteamId, byte[] Content, bool IsDefault, string ETag);
