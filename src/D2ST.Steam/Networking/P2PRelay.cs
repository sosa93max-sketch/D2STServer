using D2ST.Core.Accounts;
using D2ST.Core.Events;
using D2ST.Core.Networking;
using D2ST.Core.Steam;
using D2ST.Steam.Events;

namespace D2ST.Steam.Networking;

public sealed class P2PRelay : IP2PRelay
{
    private readonly IEventStream _events;

    public P2PRelay(IEventStream events)
    {
        _events = events;
    }

    public void Send(SteamSession session, P2PPacket packet)
    {
        if (packet.RemoteSteamId == 0)
        {
            return;
        }

        _events.Publish(SteamAccount.AccountIdFromSteamId(packet.RemoteSteamId), new SteamEvent
        {
            Type = SteamEventTypes.P2PPacket,
            // The recipient reads the sender off the event, so the packet is
            // re-addressed: its RemoteSteamId was the destination, not the source.
            SteamId = session.Account.SteamId,
            AccountId = session.Account.AccountId,
            RemoteSteamId = session.Account.SteamId,
            AppId = session.AppId,
            PayloadBase64 = packet.PayloadBase64,
            Channel = packet.Channel,
            Transport = string.IsNullOrWhiteSpace(packet.Transport) ? P2PTransports.Legacy : packet.Transport,
            VirtualPort = packet.VirtualPort,
            // Connection ids are swapped with the same reasoning: what the
            // sender called "target" is the receiver's own connection.
            SourceConnectionId = packet.TargetConnectionId,
            TargetConnectionId = packet.SourceConnectionId
        });
    }

    public void Send(SteamSession session, IEnumerable<P2PPacket> packets)
    {
        foreach (var packet in packets)
        {
            Send(session, packet);
        }
    }
}
