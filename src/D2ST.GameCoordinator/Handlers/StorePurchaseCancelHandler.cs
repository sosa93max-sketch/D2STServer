using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Cancels a pending checkout and releases its reserved local dollars.
/// </summary>
public sealed class StorePurchaseCancelHandler : IGcMessageHandler
{
    private readonly IEconomyStore _economy;

    public StorePurchaseCancelHandler(IEconomyStore economy)
    {
        _economy = economy;
    }

    public uint MessageType => GcMsg.StorePurchaseCancel;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var cancel = context.Codec.Decode<CMsgGCStorePurchaseCancel>(request.Body);
        var result = _economy.CancelPurchase(context.AccountId, cancel.TxnId);
        var response = new CMsgGCStorePurchaseCancelResponse
        {
            Result = result.Success
                ? (uint)StorePurchaseWireResult.Success
                : (uint)StorePurchaseWireResult.Failure
        };

        return
        [
            new GcMessage(GcMsg.StorePurchaseCancelResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
