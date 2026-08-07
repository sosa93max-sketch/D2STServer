using D2ST.Core.Accounts;
using D2ST.Core.Events;
using D2ST.Core.GameCoordinator;
using D2ST.Core.Steam;
using D2ST.GameCoordinator.Messaging;
using D2ST.Protocol.Dota;
using D2ST.Steam;
using D2ST.Steam.Events;

namespace D2ST.Api;

/// <summary>
/// Sends a GC message to a player over the channel its process actually drains.
/// <para>
/// A Dota client never calls <c>/api/gamecoordinator/poll</c>: the shim feeds
/// the game from its event pump and only a logged-on dedicated server drains the
/// poll channel. So a message for a live client session travels as a
/// <c>gc_message</c> event, which the pump is already long-polling for and
/// replays into the game as an unsolicited GC message; anything else (a
/// dedicated server, or a player whose client is gone) waits in the queue.
/// </para>
/// </summary>
public sealed class EventStreamGcMessageQueue : IGcMessageQueue
{
    private readonly GcMessageQueue _pending;
    private readonly IEventStream _events;
    private readonly ISessionStore _sessions;

    public EventStreamGcMessageQueue(GcMessageQueue pending, IEventStream events, ISessionStore sessions)
    {
        _pending = pending;
        _events = events;
        _sessions = sessions;
    }

    public void Enqueue(uint accountId, GcMessage message)
    {
        if (!_sessions.IsOnline(accountId))
        {
            _pending.Enqueue(accountId, message);
            return;
        }

        _events.Publish(
            accountId,
            new SteamEvent
            {
                Type = SteamEventTypes.GcMessage,
                AccountId = accountId,
                AppId = DotaApp.AppId,
                MessageType = message.MessageType,
                TargetJobId = message.TargetJobId,
                Protobuf = true,
                PayloadBase64 = Convert.ToBase64String(message.Body)
            },
            ProcessRoles.Client);
    }

    public void EnqueueToSteamId(ulong steamId, GcMessage message) =>
        Enqueue(SteamAccount.AccountIdFromSteamId(steamId), message);

    public IReadOnlyList<GcMessage> Drain(uint accountId) => _pending.Drain(accountId);
}
