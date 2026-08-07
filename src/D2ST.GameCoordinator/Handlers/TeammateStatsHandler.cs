using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There is no match data for teammate stats (8124 → 8125, success, empty).
/// </summary>
public sealed class TeammateStatsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCTeammateStatsRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCTeammateStatsResponse { Success = true };
        return
        [
            new GcMessage(GcMsg.ClientToGCTeammateStatsResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
