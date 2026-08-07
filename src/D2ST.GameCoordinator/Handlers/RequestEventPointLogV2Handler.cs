using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There are no event points to log (8298 → 8299, success).
/// </summary>
public sealed class RequestEventPointLogV2Handler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCRequestEventPointLogV2;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCRequestEventPointLogResponseV2 { Result = true };
        return
        [
            new GcMessage(GcMsg.ClientToGCRequestEventPointLogResponseV2,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
