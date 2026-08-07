using System.Collections.Concurrent;
using D2ST.Core.Accounts;
using D2ST.Core.GameCoordinator;

namespace D2ST.GameCoordinator.Messaging;

/// <summary>
/// Sends a GC message to a player who did not ask for it. Handlers reply to
/// their caller by returning messages; everything that has to reach *another*
/// player (a party invite, a lobby update, a chat line) goes through here and is
/// delivered on that player's next poll.
/// </summary>
public interface IGcMessageQueue
{
    void Enqueue(uint accountId, GcMessage message);

    void EnqueueToSteamId(ulong steamId, GcMessage message);

    /// <summary>Takes everything queued for an account, leaving the queue empty.</summary>
    IReadOnlyList<GcMessage> Drain(uint accountId);
}

public sealed class GcMessageQueue : IGcMessageQueue
{
    private readonly ConcurrentDictionary<uint, ConcurrentQueue<GcMessage>> _pending = new();

    public void Enqueue(uint accountId, GcMessage message) =>
        _pending.GetOrAdd(accountId, static _ => new ConcurrentQueue<GcMessage>()).Enqueue(message);

    public void EnqueueToSteamId(ulong steamId, GcMessage message) =>
        Enqueue(SteamAccount.AccountIdFromSteamId(steamId), message);

    public IReadOnlyList<GcMessage> Drain(uint accountId)
    {
        if (!_pending.TryGetValue(accountId, out var queue))
        {
            return [];
        }

        var drained = new List<GcMessage>();
        while (queue.TryDequeue(out var message))
        {
            drained.Add(message);
        }

        return drained;
    }
}
