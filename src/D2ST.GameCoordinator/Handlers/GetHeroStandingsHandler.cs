using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There is no ranked ladder to stand on (7274 → 7275 with an empty list).
/// </summary>
public sealed class GetHeroStandingsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.GCGetHeroStandings;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgGCGetHeroStandingsResponse();
        return
        [
            new GcMessage(GcMsg.GCGetHeroStandingsResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
