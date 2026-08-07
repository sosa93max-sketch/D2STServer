using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There are no store transactions in flight, so the cancellation (2617 → 2618)
/// is trivially OK.
/// </summary>
public sealed class CancelUnfinalizedTransactionsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCCancelUnfinalizedTransactions;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCCancelUnfinalizedTransactionsResponse { Result = 1 };
        return
        [
            new GcMessage(GcMsg.ClientToGCCancelUnfinalizedTransactionsResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
