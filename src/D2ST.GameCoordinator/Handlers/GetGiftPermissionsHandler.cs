using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The local deployment has no Steam trade restrictions. Returning explicit
/// permissions keeps the gift/subscription panels from waiting on an
/// unhandled 8126 request.
/// </summary>
public sealed class GetGiftPermissionsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetGiftPermissions;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _ = context.Codec.Decode<CMsgClientToGCGetGiftPermissions>(request.Body);
        var response = new CMsgClientToGCGetGiftPermissionsResponse
        {
            IsUnlimited = true,
            HasTwoFactor = true,
            SenderPermission = EGCMsgInitiateTradeResponse.kEGCMsgInitiateTradeResponseAccepted,
            FriendshipAgeRequirement = 0,
            FriendshipAgeRequirementTwoFactor = 0
        };

        return
        [
            new GcMessage(
                GcMsg.ClientToGCGetGiftPermissionsResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
