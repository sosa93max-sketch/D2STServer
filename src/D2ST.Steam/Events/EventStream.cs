using D2ST.Core.Events;
using D2ST.Core.Steam;

namespace D2ST.Steam.Events;

/// <summary>
/// Bounded in-memory event log with a monotonic cursor. Clients ask for
/// everything after the cursor they last saw, so a client that misses a poll
/// (or reconnects) catches up instead of losing events, and a slow client only
/// loses the oldest ones once the log wraps.
/// </summary>
public sealed class EventStream : IEventStream
{
    private const int Capacity = 4096;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    private readonly object _sync = new();
    private readonly Queue<QueuedEvent> _events = new();
    private long _sequence;
    private TaskCompletionSource _published = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Publish(uint recipientAccountId, SteamEvent steamEvent, string? processRole = null)
    {
        TaskCompletionSource published;
        lock (_sync)
        {
            _events.Enqueue(new QueuedEvent(++_sequence, recipientAccountId, processRole, steamEvent));
            while (_events.Count > Capacity)
            {
                _events.Dequeue();
            }

            published = _published;
            _published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        published.SetResult();
    }

    public async Task<EventBatch> ReadAsync(
        SteamSession session,
        long cursor,
        TimeSpan wait,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + (wait > MaxWait ? MaxWait : wait);
        while (true)
        {
            Task published;
            lock (_sync)
            {
                var matched = _events
                    .Where(queued => queued.Sequence > cursor && queued.IsFor(session))
                    .ToList();

                if (matched.Count > 0)
                {
                    return new EventBatch(
                        matched[^1].Sequence,
                        matched.Select(queued => queued.Event).ToList());
                }

                published = _published.Task;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero || cancellationToken.IsCancellationRequested)
            {
                return new EventBatch(cursor, Array.Empty<SteamEvent>());
            }

            await Task.WhenAny(published, Task.Delay(remaining, cancellationToken)).ConfigureAwait(false);
        }
    }

    private sealed record QueuedEvent(long Sequence, uint RecipientAccountId, string? ProcessRole, SteamEvent Event)
    {
        public bool IsFor(SteamSession session) =>
            (RecipientAccountId == 0 || RecipientAccountId == session.Account.AccountId) &&
            (string.IsNullOrEmpty(ProcessRole) || ProcessRole == session.ProcessRole);
    }
}
