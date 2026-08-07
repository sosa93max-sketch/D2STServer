using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There are no league matches to show (8036 → 8061 with an empty list).
/// </summary>
public sealed class TopLeagueMatchesHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCTopLeagueMatchesRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgGCToClientTopLeagueMatchesResponse();
        return
        [
            new GcMessage(GcMsg.GCToClientTopLeagueMatchesResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
