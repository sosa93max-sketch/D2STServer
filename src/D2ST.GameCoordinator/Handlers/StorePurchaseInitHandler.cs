using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;
using Microsoft.Extensions.Logging;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Starts a store checkout. The API-backed economy reserves the local dollars
/// and returns a transaction id; finalization performs the durable grant.
/// </summary>
public sealed class StorePurchaseInitHandler : IGcMessageHandler
{
    private readonly IEconomyStore _economy;
    private readonly ILogger<StorePurchaseInitHandler> _logger;

    public StorePurchaseInitHandler(
        IEconomyStore economy,
        ILogger<StorePurchaseInitHandler> logger)
    {
        _economy = economy;
        _logger = logger;
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
            if (product is null)
            {
                _logger.LogWarning(
                    "Rechazo de compra local: cuenta {AccountId}, item_def {ItemDefId} no existe o está inactivo",
                    context.AccountId,
                    line.ItemDefId);
                valid = false;
                break;
            }

            // quantity is optional in the protobuf. Dota sends it for normal
            // line items, but treating an omitted value as one keeps a single
            // item checkout compatible with clients that omit the field.
            var quantity = line.Quantity == 0 ? 1u : line.Quantity;

            // Never trust the client-reported cost: BeginPurchase re-reads the
            // active catalog price. This log only makes a stale sale cache
            // visible while keeping the server authoritative.
            if (line.CostInLocalCurrency != 0 &&
                line.CostInLocalCurrency != LocalEconomyCurrency.ToWireAmount(product.PriceDollars))
            {
                _logger.LogDebug(
                    "Precio de cliente desactualizado: cuenta {AccountId}, producto {ProductId}, cliente {ClientPrice}, catálogo {CatalogPrice}",
                    context.AccountId,
                    product.ProductId,
                    line.CostInLocalCurrency,
                    product.PriceDollars);
            }

            lines.Add(new StorePurchaseLine(product.ProductId, quantity));
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

        _logger.LogInformation(
            "Compra local init: cuenta {AccountId}, líneas {LineCount}, resultado {ResultCode}, transacción {TransactionId}, saldo {BalanceDollars}, disponible {AvailableDollars}",
            context.AccountId,
            lines.Count,
            result.Code,
            result.TransactionId,
            result.Wallet.BalanceDollars,
            result.Wallet.AvailableDollars);

        return
        [
            new GcMessage(GcMsg.StorePurchaseInitResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
