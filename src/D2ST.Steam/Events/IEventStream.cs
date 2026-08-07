using D2ST.Core.Events;
using D2ST.Core.Steam;

namespace D2ST.Steam.Events;

/// <summary>
/// Server → client push channel. The shim has no inbound socket, so everything
/// the server wants to tell a client (presence, friends, lobbies, GC messages)
/// is queued here and drained by its long-polling event pump.
/// </summary>
public interface IEventStream
{
    /// <summary>
    /// Queues an event for one account, or for every session when
    /// <paramref name="recipientAccountId"/> is 0.
    /// </summary>
    void Publish(uint recipientAccountId, SteamEvent steamEvent, string? processRole = null);

    /// <summary>
    /// Returns the events after <paramref name="cursor"/> for the session,
    /// waiting up to <paramref name="wait"/> for the first one to show up.
    /// </summary>
    Task<EventBatch> ReadAsync(
        SteamSession session,
        long cursor,
        TimeSpan wait,
        CancellationToken cancellationToken = default);
}

/// <param name="Cursor">Cursor to send on the next read.</param>
public sealed record EventBatch(long Cursor, IReadOnlyList<SteamEvent> Events);
