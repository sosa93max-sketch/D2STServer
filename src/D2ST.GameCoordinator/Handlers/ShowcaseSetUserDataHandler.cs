using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The showcase is not persisted yet; accepting the write (8888 → 8889) with
/// success keeps the client from treating it as a failure.
/// </summary>
public sealed class ShowcaseSetUserDataHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCShowcaseSetUserData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCShowcaseSetUserDataResponse
        {
            Response = CMsgClientToGCShowcaseSetUserDataResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCShowcaseSetUserDataResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
