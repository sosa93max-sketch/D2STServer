using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The social feed is empty (8303 → 8304, success with no events).
/// </summary>
public sealed class RequestSocialFeedHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCRequestSocialFeed;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgSocialFeedResponse();
        return
        [
            new GcMessage(GcMsg.ClientToGCRequestSocialFeedResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
