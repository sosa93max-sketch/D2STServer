using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Opens a treasure. There is no drop table, so no item is granted and the
/// client is told the request was denied rather than being handed an empty
/// success it would try to animate.
/// </summary>
public sealed class UnlockCrateHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.UnlockCrate;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _ = context.Codec.Decode<CMsgClientToGCUnlockCrate>(request.Body);
        var response = new CMsgClientToGCUnlockCrateResponse
        {
            Result = EGCMsgResponse.kEGCMsgResponseDenied
        };

        return
        [
            new GcMessage(GcMsg.UnlockCrateResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
