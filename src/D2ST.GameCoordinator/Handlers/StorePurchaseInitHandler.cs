using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Starts a store checkout. The API-backed economy reserves the local credits
/// and returns a transaction id; finalization performs the durable grant.
/// </summary>
public sealed class StorePurchaseInitHandler : IGcMessageHandler
{
    private readonly IEconomyStore _economy;

    public StorePurchaseInitHandler(IEconomyStore economy)
    {
        _economy = economy;
    }

    public uint MessageType => GcMsg.StorePurchaseInit;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var purchase = context.Codec.Decode<CMsgGCStorePurchaseInit>(request.Body);
        var lines = new List<StorePurchaseLine>(purchase.LineItems.Count);
        var valid = purchase.LineItems.Count != 0;
        foreach (var line in purchase.LineItems)
        {
            var product = _economy.FindProduct(line.ItemDefId);
            if (product is null || line.Quantity == 0)
            {
                valid = false;
                break;
            }

            lines.Add(new StorePurchaseLine(product.ProductId, line.Quantity));
        }

        var result = valid
            ? _economy.BeginPurchase(context.AccountId, lines)
            : StoreOperationResult.Failed("invalid_purchase", "La compra no es válida.");
        var response = new CMsgGCStorePurchaseInitResponse
        {
            Result = result.Success
                ? (int)EGCMsgResponse.kEGCMsgResponseOK
                : (int)EGCMsgResponse.kEGCMsgResponseDenied,
            TxnId = result.Success ? result.TransactionId : 0
        };

        return
        [
            new GcMessage(GcMsg.StorePurchaseInitResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
