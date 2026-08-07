using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Messaging;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator;

/// <summary>
/// Coordinates the request/response exchange with a client. A client posts a GC
/// message (exchange); the router produces the immediate responses, and the
/// exchange also carries whatever the server pushed to that player — its own
/// Shared Object deltas included — so a client never has to wait for a poll to
/// see the effect of what it just asked for. Anything queued while the player is
/// sending nothing still waits in <see cref="IGcMessageQueue"/> for the poll.
/// </summary>
public sealed class GameCoordinatorService
{
    private readonly GcRouter _router;
    private readonly IGcMessageQueue _queue;

    public GameCoordinatorService(GcRouter router, IGcMessageQueue queue)
    {
        _router = router;
        _queue = queue;
    }

    public IReadOnlyList<GcMessage> Exchange(GcContext context, GcMessage request)
    {
        var replies = _router.Dispatch(context, request);
        var pushed = _queue.Drain(context.AccountId);
        if (pushed.Count == 0)
        {
            return replies;
        }

        // A cache reaches the client before the reply that refers to it: the
        // client draws the lobby or party it was just told it created from the
        // Shared Object, not from the reply, and treats a success it cannot draw
        // as a failure. The welcome is the exception — nothing may precede the
        // session it opens.
        return request.MessageType == GcMsg.ClientHello
            ? [.. replies, .. pushed]
            : [.. pushed, .. replies];
    }

    /// <summary>Whether a handler is registered for a message type.</summary>
    public bool CanHandle(uint messageType) => _router.CanHandle(messageType);

    /// <summary>Queues a message to be delivered to an account on its next poll.</summary>
    public void Enqueue(uint accountId, GcMessage message) => _queue.Enqueue(accountId, message);

    public IReadOnlyList<GcMessage> Poll(uint accountId) => _queue.Drain(accountId);
}
