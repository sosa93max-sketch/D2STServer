using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Redeems an item against an in-game currency (the compendium shops of the
/// era). No currency is tracked, so redemption fails.
/// </summary>
public sealed class RedeemItemHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.RedeemItem;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _ = context.Codec.Decode<CMsgDOTARedeemItem>(request.Body);
        var response = new CMsgDOTARedeemItemResponse
        {
            Response = CMsgDOTARedeemItemResponse.EResultCode.kFailed
        };

        return
        [
            new GcMessage(GcMsg.RedeemItemResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
