using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No hero stickers are owned (8853 → 8854, success, empty).
/// </summary>
public sealed class GetHeroStickersHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetHeroStickers;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCGetHeroStickersResponse
        {
            Response = CMsgClientToGCGetHeroStickersResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCGetHeroStickersResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
