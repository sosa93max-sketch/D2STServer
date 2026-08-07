using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The hero grid keeps the default order (7606 → 7607 with no explicit ids).
/// </summary>
public sealed class GetAllHeroOrderHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetAllHeroOrder;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCGetAllHeroOrderResponse();
        return
        [
            new GcMessage(GcMsg.ClientToGCGetAllHeroOrderResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
