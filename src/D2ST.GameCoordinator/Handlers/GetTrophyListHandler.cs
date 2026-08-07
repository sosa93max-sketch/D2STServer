using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No trophies have been earned (7527 → 7528 with an empty list).
/// </summary>
public sealed class GetTrophyListHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetTrophyList;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCGetTrophyListResponse();
        return
        [
            new GcMessage(GcMsg.ClientToGCGetTrophyListResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
