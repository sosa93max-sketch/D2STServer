using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Cancels a checkout. No transaction is ever open, so cancelling always
/// succeeds and clears the client's pending purchase state.
/// </summary>
public sealed class StorePurchaseCancelHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.StorePurchaseCancel;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _ = context.Codec.Decode<CMsgGCStorePurchaseCancel>(request.Body);
        var response = new CMsgGCStorePurchaseCancelResponse
        {
            Result = (uint)EGCMsgResponse.kEGCMsgResponseOK
        };

        return
        [
            new GcMessage(GcMsg.StorePurchaseCancelResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
