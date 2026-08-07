using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No friend matches are live (8037 → 8062 with an empty list).
/// </summary>
public sealed class TopFriendMatchesHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCTopFriendMatchesRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgGCToClientTopFriendMatchesResponse();
        return
        [
            new GcMessage(GcMsg.GCToClientTopFriendMatchesResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
