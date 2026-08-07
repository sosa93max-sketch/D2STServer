using D2ST.Core.Social;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Steam.Social;

/// <summary>
/// Relationship reads shared by the directory and the friend service: who is a
/// friend of whom, and which invitations are still open.
/// </summary>
public sealed class FriendGraph
{
    private readonly D2stDbContext _db;

    public FriendGraph(D2stDbContext db)
    {
        _db = db;
    }

    public Task<List<uint>> FriendIdsAsync(uint accountId, CancellationToken cancellationToken) =>
        _db.Friendships
            .Where(friendship => friendship.AccountId == accountId)
            .Select(friendship => friendship.FriendAccountId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns every account that has a visible relationship with the player:
    /// confirmed friends plus either side of a pending invitation. The shim's
    /// full friends refresh replaces its local cache, so pending invitations
    /// must be part of this snapshot as well as the pushed event stream.
    /// </summary>
    public async Task<List<uint>> RelatedIdsAsync(
        uint accountId,
        CancellationToken cancellationToken)
    {
        var ids = await FriendIdsAsync(accountId, cancellationToken);

        var pendingIds = await _db.FriendRequests
            .Where(request => request.Status == FriendRequestStatus.Pending &&
                (request.FromAccountId == accountId || request.ToAccountId == accountId))
            .Select(request => request.FromAccountId == accountId
                ? request.ToAccountId
                : request.FromAccountId)
            .ToListAsync(cancellationToken);

        return ids
            .Concat(pendingIds)
            .Where(id => id != accountId)
            .Distinct()
            .ToList();
    }

    public Task<FriendRequestEntity?> FindPendingAsync(uint fromAccountId, uint toAccountId, CancellationToken cancellationToken) =>
        _db.FriendRequests.FirstOrDefaultAsync(
            request => request.Status == FriendRequestStatus.Pending &&
                request.FromAccountId == fromAccountId &&
                request.ToAccountId == toAccountId,
            cancellationToken);

    public async Task<FriendRelationship> RelationshipAsync(
        uint viewerAccountId,
        uint accountId,
        CancellationToken cancellationToken)
    {
        if (viewerAccountId == accountId)
        {
            return FriendRelationship.Friend;
        }

        var friends = await _db.Friendships.AnyAsync(
            friendship => friendship.AccountId == viewerAccountId && friendship.FriendAccountId == accountId,
            cancellationToken);
        if (friends)
        {
            return FriendRelationship.Friend;
        }

        var pending = await _db.FriendRequests
            .Where(request => request.Status == FriendRequestStatus.Pending &&
                ((request.FromAccountId == accountId && request.ToAccountId == viewerAccountId) ||
                 (request.FromAccountId == viewerAccountId && request.ToAccountId == accountId)))
            .Select(request => request.FromAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (pending == accountId)
        {
            return FriendRelationship.RequestRecipient;
        }

        return pending == viewerAccountId ? FriendRelationship.RequestInitiator : FriendRelationship.None;
    }

    /// <summary>
    /// Everyone who must hear about a change to this player: their friends and
    /// anyone with an open invitation either way, plus the player themselves
    /// (their own other clients need the update too).
    /// </summary>
    public async Task<IReadOnlyCollection<uint>> AudienceAsync(uint accountId, CancellationToken cancellationToken)
    {
        var audience = new HashSet<uint> { accountId };
        audience.UnionWith(await FriendIdsAsync(accountId, cancellationToken));

        var pending = await _db.FriendRequests
            .Where(request => request.Status == FriendRequestStatus.Pending &&
                (request.FromAccountId == accountId || request.ToAccountId == accountId))
            .Select(request => new { request.FromAccountId, request.ToAccountId })
            .ToListAsync(cancellationToken);

        foreach (var request in pending)
        {
            audience.Add(request.FromAccountId == accountId ? request.ToAccountId : request.FromAccountId);
        }

        return audience;
    }
}
