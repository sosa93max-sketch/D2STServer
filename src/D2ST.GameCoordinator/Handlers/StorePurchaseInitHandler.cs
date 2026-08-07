using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Starts a store checkout. Nothing is for sale, so the purchase is refused
/// with a zero transaction; letting it succeed would leave the client waiting
/// for items that never arrive in the econ cache.
/// </summary>
public sealed class StorePurchaseInitHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.StorePurchaseInit;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _ = context.Codec.Decode<CMsgGCStorePurchaseInit>(request.Body);
        var response = new CMsgGCStorePurchaseInitResponse
        {
            Result = (int)EGCMsgResponse.kEGCMsgResponseDenied,
            TxnId = 0
        };

        return
        [
            new GcMessage(GcMsg.StorePurchaseInitResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
