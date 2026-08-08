using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;
using Microsoft.Extensions.Logging;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Cancels a pending checkout and releases its reserved local dollars.
/// </summary>
public sealed class StorePurchaseCancelHandler : IGcMessageHandler
{
    private readonly IEconomyStore _economy;
    private readonly ILogger<StorePurchaseCancelHandler> _logger;

    public StorePurchaseCancelHandler(
        IEconomyStore economy,
        ILogger<StorePurchaseCancelHandler> logger)
    {
        _economy = economy;
        _logger = logger;
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

        _logger.LogInformation(
            "Compra local cancel: cuenta {AccountId}, transacción {TransactionId}, resultado {ResultCode}, saldo {BalanceDollars}, disponible {AvailableDollars}",
            context.AccountId,
            cancel.TxnId,
            result.Code,
            result.Wallet.BalanceDollars,
            result.Wallet.AvailableDollars);

        return
        [
            new GcMessage(GcMsg.StorePurchaseCancelResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
