using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There are no active quests (8078 → 8079, success with an empty list).
/// </summary>
public sealed class GetQuestProgressHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetQuestProgress;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCGetQuestProgressResponse { Success = true };
        return
        [
            new GcMessage(GcMsg.ClientToGCGetQuestProgressResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
