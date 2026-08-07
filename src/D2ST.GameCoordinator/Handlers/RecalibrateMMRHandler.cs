using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There is no MMR to recalibrate (8759 → 8760, success).
/// </summary>
public sealed class RecalibrateMMRHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCRecalibrateMMR;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCRecalibrateMMRResponse
        {
            Result = CMsgClientToGCRecalibrateMMRResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCRecalibrateMMRResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
