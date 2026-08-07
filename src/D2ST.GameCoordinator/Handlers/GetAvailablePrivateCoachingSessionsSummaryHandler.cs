using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No coaches are offering sessions (8800 → 8801, success, empty summary).
/// </summary>
public sealed class GetAvailablePrivateCoachingSessionsSummaryHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetAvailablePrivateCoachingSessionsSummary;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCGetAvailablePrivateCoachingSessionsSummaryResponse
        {
            Result = CMsgClientToGCGetAvailablePrivateCoachingSessionsSummaryResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCGetAvailablePrivateCoachingSessionsSummaryResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
