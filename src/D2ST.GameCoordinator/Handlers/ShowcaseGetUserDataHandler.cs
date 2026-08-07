using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The profile showcase has nothing configured (8886 → 8887, success, empty).
/// </summary>
public sealed class ShowcaseGetUserDataHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCShowcaseGetUserData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCShowcaseGetUserDataResponse
        {
            Response = CMsgClientToGCShowcaseGetUserDataResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCShowcaseGetUserDataResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
