using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the rank request (8879 → 8880). There is no ranked ladder here yet,
/// so the reply is success with a zeroed rank.
/// </summary>
public sealed class RankRequestHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCRankRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgGCToClientRankResponse
        {
            Result = CMsgGCToClientRankResponse.EResultCode.kSucceeded
        };

        return
        [
            new GcMessage(GcMsg.GCToClientRankResponse, context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
