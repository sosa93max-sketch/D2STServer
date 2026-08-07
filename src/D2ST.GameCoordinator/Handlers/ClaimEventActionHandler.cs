using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There are no events with claimable actions (8209 → 8210, success, no
/// rewards).
/// </summary>
public sealed class ClaimEventActionHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.DOTAClaimEventAction;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgDOTAClaimEventActionResponse
        {
            Result = CMsgDOTAClaimEventActionResponse.ResultCode.Success
        };

        return
        [
            new GcMessage(GcMsg.DOTAClaimEventActionResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
