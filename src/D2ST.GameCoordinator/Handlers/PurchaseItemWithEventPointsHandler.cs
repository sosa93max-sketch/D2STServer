using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Buys an event item with battle pass points. Every account has zero points
/// (see <see cref="EventPointsHandler"/>), so the purchase is refused for lack
/// of points rather than as a generic error.
/// </summary>
public sealed class PurchaseItemWithEventPointsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.PurchaseItemWithEventPoints;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _ = context.Codec.Decode<CMsgPurchaseItemWithEventPoints>(request.Body);
        var response = new CMsgPurchaseItemWithEventPointsResponse
        {
            result = CMsgPurchaseItemWithEventPointsResponse.Result.NotEnoughPoints
        };

        return
        [
            new GcMessage(
                GcMsg.PurchaseItemWithEventPointsResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
