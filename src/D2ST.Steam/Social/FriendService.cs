using D2ST.Core.Events;
using D2ST.Core.Social;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Steam.Social;

public sealed class FriendService : IFriendService
{
    private readonly D2stDbContext _db;
    private readonly FriendGraph _graph;
    private readonly SocialEventPublisher _publisher;
    private readonly TimeProvider _time;

    public FriendService(
        D2stDbContext db,
        FriendGraph graph,
        SocialEventPublisher publisher,
        TimeProvider time)
    {
        _db = db;
        _graph = graph;
        _publisher = publisher;
        _time = time;
    }

    public async Task<bool> RequestAsync(
        uint accountId,
        uint targetAccountId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == targetAccountId || !await BothExistAsync(accountId, targetAccountId, cancellationToken))
        {
            return false;
        }

        if (await AreFriendsAsync(accountId, targetAccountId, cancellationToken))
        {
            return true;
        }

        // Inviting someone who already invited you is an accept, not a second
        // invitation: this is how the client's "add friend" button behaves.
        var reverse = await _graph.FindPendingAsync(targetAccountId, accountId, cancellationToken);
        if (reverse is not null)
        {
            return await AcceptAsync(reverse, cancellationToken);
        }

        var existing = await _graph.FindPendingAsync(accountId, targetAccountId, cancellationToken);
        if (existing is not null)
        {
            return true;
        }

        var request = new FriendRequestEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            FromAccountId = accountId,
            ToAccountId = targetAccountId,
            Status = FriendRequestStatus.Pending,
            CreatedAt = _time.GetUtcNow()
        };

        _db.FriendRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        await _publisher.PublishRelationshipAsync(
            targetAccountId,
            accountId,
            SteamEventTypes.FriendRequestReceived,
            FriendRelationship.RequestRecipient,
            request.Id,
            cancellationToken);
        await _publisher.PublishRelationshipAsync(
            accountId,
            targetAccountId,
            SteamEventTypes.FriendRequestSent,
            FriendRelationship.RequestInitiator,
            request.Id,
            cancellationToken);
        return true;
    }

    public async Task<bool> AcceptAsync(
        uint accountId,
        uint fromAccountId,
        CancellationToken cancellationToken = default)
    {
        var request = await _graph.FindPendingAsync(fromAccountId, accountId, cancellationToken);
        return request is not null && await AcceptAsync(request, cancellationToken);
    }

    public async Task<bool> RemoveAsync(
        uint accountId,
        uint otherAccountId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == otherAccountId)
        {
            return false;
        }

        var links = await _db.Friendships
            .Where(friendship =>
                (friendship.AccountId == accountId && friendship.FriendAccountId == otherAccountId) ||
                (friendship.AccountId == otherAccountId && friendship.FriendAccountId == accountId))
            .ToListAsync(cancellationToken);

        var pending = await _db.FriendRequests
            .Where(request => request.Status == FriendRequestStatus.Pending &&
                ((request.FromAccountId == accountId && request.ToAccountId == otherAccountId) ||
                 (request.FromAccountId == otherAccountId && request.ToAccountId == accountId)))
            .ToListAsync(cancellationToken);

        if (links.Count == 0 && pending.Count == 0)
        {
            return false;
        }

        _db.Friendships.RemoveRange(links);
        foreach (var request in pending)
        {
            request.Status = request.FromAccountId == accountId
                ? FriendRequestStatus.Cancelled
                : FriendRequestStatus.Declined;
            request.RespondedAt = _time.GetUtcNow();
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _publisher.PublishRelationshipAsync(
            accountId,
            otherAccountId,
            SteamEventTypes.FriendRemoved,
            FriendRelationship.None,
            requestId: string.Empty,
            cancellationToken);
        await _publisher.PublishRelationshipAsync(
            otherAccountId,
            accountId,
            SteamEventTypes.FriendRemoved,
            FriendRelationship.None,
            requestId: string.Empty,
            cancellationToken);
        return true;
    }

    private async Task<bool> AcceptAsync(FriendRequestEntity request, CancellationToken cancellationToken)
    {
        request.Status = FriendRequestStatus.Accepted;
        request.RespondedAt = _time.GetUtcNow();

        var createdAt = _time.GetUtcNow();
        _db.Friendships.Add(new FriendshipEntity
        {
            AccountId = request.FromAccountId,
            FriendAccountId = request.ToAccountId,
            CreatedAt = createdAt
        });
        _db.Friendships.Add(new FriendshipEntity
        {
            AccountId = request.ToAccountId,
            FriendAccountId = request.FromAccountId,
            CreatedAt = createdAt
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _publisher.PublishRelationshipAsync(
            request.FromAccountId,
            request.ToAccountId,
            SteamEventTypes.FriendAdded,
            FriendRelationship.Friend,
            request.Id,
            cancellationToken);
        await _publisher.PublishRelationshipAsync(
            request.ToAccountId,
            request.FromAccountId,
            SteamEventTypes.FriendAdded,
            FriendRelationship.Friend,
            request.Id,
            cancellationToken);
        return true;
    }

    private Task<bool> AreFriendsAsync(uint accountId, uint otherAccountId, CancellationToken cancellationToken) =>
        _db.Friendships.AnyAsync(
            friendship => friendship.AccountId == accountId && friendship.FriendAccountId == otherAccountId,
            cancellationToken);

    private async Task<bool> BothExistAsync(uint accountId, uint otherAccountId, CancellationToken cancellationToken) =>
        await _db.Accounts.CountAsync(
            account => account.AccountId == accountId || account.AccountId == otherAccountId,
            cancellationToken) == 2;
}
