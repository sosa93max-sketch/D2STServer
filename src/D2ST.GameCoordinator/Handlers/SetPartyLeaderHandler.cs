using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Hands the party over to another member (7588). Leader only.</summary>
public sealed class SetPartyLeaderHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public SetPartyLeaderHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.SetPartyLeader;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var setLeader = context.Codec.Decode<CMsgDOTASetGroupLeader>(request.Body);
        _parties.SetLeader(context, setLeader.NewLeaderSteamid);
        return [];
    }
}
