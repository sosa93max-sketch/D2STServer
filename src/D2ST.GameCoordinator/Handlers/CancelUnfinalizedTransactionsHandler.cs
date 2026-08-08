using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Releases any local-store reservations that survived a client disconnect,
/// then acknowledges the cancellation (2617 → 2618).
/// </summary>
public sealed class CancelUnfinalizedTransactionsHandler : IGcMessageHandler
{
    private readonly IEconomyStore _economy;

    public CancelUnfinalizedTransactionsHandler(IEconomyStore economy)
    {
        _economy = economy;
    }

    public uint MessageType => GcMsg.ClientToGCCancelUnfinalizedTransactions;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _economy.CancelPendingPurchases(context.AccountId);
        var response = new CMsgClientToGCCancelUnfinalizedTransactionsResponse { Result = 1 };
        return
        [
            new GcMessage(GcMsg.ClientToGCCancelUnfinalizedTransactionsResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
