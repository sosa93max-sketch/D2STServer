using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Stores the region pings a client measured (8068). Outside a party there is
/// nothing to store them on: matchmaking reads them from the party object.
/// </summary>
public sealed class ClientPingDataHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public ClientPingDataHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.ClientToGCPingData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _parties.SetPingData(context, context.Codec.Decode<CMsgClientPingData>(request.Body));
        return [];
    }
}
