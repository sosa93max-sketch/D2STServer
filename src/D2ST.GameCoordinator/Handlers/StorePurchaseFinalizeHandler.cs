using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.Protocol.Dota;
using Microsoft.Extensions.Logging;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Commits the wallet debit and publishes the purchased econ items after the
/// client's store checkout reaches its finalize step.
/// </summary>
public sealed class StorePurchaseFinalizeHandler : IGcMessageHandler
{
    private readonly IEconomyStore _economy;
    private readonly EconInventory _inventory;
    private readonly DotaPlusProjection _dotaPlus;
    private readonly ILogger<StorePurchaseFinalizeHandler> _logger;

    public StorePurchaseFinalizeHandler(
        IEconomyStore economy,
        EconInventory inventory,
        DotaPlusProjection dotaPlus,
        ILogger<StorePurchaseFinalizeHandler> logger)
    {
        _economy = economy;
        _inventory = inventory;
        _dotaPlus = dotaPlus;
        _logger = logger;
    }

    public uint MessageType => GcMsg.StorePurchaseFinalize;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var finalize = context.Codec.Decode<CMsgGCStorePurchaseFinalize>(request.Body);
        var result = _economy.FinalizePurchase(context.AccountId, finalize.TxnId);

        _logger.LogInformation(
            "Compra local finalize: cuenta {AccountId}, transacción {TransactionId}, resultado {ResultCode}, saldo {BalanceCredits}, disponible {AvailableCredits}",
            context.AccountId,
            finalize.TxnId,
            result.Code,
            result.Wallet.BalanceCredits,
            result.Wallet.AvailableCredits);

        if (result.Success)
        {
            _inventory.ApplyItems(context.SteamId, context.AccountId, result.Items);
            _dotaPlus.Refresh(context.AccountId);
        }

        var response = new CMsgGCStorePurchaseFinalizeResponse
        {
            Result = result.Success
                ? (uint)EGCMsgResponse.kEGCMsgResponseOK
                : (uint)EGCMsgResponse.kEGCMsgResponseDenied,
            ItemIds = result.Success ? result.ItemIds.ToArray() : Array.Empty<ulong>()
        };

        return
        [
            new GcMessage(
                GcMsg.StorePurchaseFinalizeResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
