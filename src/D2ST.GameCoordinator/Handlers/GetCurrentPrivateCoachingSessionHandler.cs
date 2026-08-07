using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No private coaching is implemented, so the answer (8793 → 8794) is success
/// with no session.
/// </summary>
public sealed class GetCurrentPrivateCoachingSessionHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetCurrentPrivateCoachingSession;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCGetCurrentPrivateCoachingSessionResponse
        {
            Result = CMsgClientToGCGetCurrentPrivateCoachingSessionResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCGetCurrentPrivateCoachingSessionResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
