using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The notification feed has nothing in it (7427 → 7428 with an empty update).
/// </summary>
public sealed class NotificationsRequestHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.GCNotificationsRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgGCNotificationsResponse
        {
            Update = new CMsgGCNotificationsUpdate
            {
                Result = CMsgGCNotificationsUpdate.EResult.Success
            }
        };

        return
        [
            new GcMessage(GcMsg.GCNotificationsResponse, context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
