using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;
using Microsoft.Extensions.Logging;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Releases any local-store reservations that survived a client disconnect,
/// then acknowledges the cancellation (2617 → 2618).
/// </summary>
public sealed class CancelUnfinalizedTransactionsHandler : IGcMessageHandler
{
    private readonly IEconomyStore _economy;
    private readonly ILogger<CancelUnfinalizedTransactionsHandler> _logger;

    public CancelUnfinalizedTransactionsHandler(
        IEconomyStore economy,
        ILogger<CancelUnfinalizedTransactionsHandler> logger)
    {
        _economy = economy;
        _logger = logger;
    }

    public uint MessageType => GcMsg.ClientToGCCancelUnfinalizedTransactions;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var cancelled = _economy.CancelPendingPurchases(context.AccountId);
        _logger.LogInformation(
            "Compras locales pendientes canceladas: cuenta {AccountId}, cantidad {Count}",
            context.AccountId,
            cancelled);
        var response = new CMsgClientToGCCancelUnfinalizedTransactionsResponse { Result = 1 };
        return
        [
            new GcMessage(GcMsg.ClientToGCCancelUnfinalizedTransactionsResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
