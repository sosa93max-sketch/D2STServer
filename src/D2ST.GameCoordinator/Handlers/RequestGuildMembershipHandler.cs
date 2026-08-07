using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The player is in no guild (8676 → 8677, success with empty memberships).
/// </summary>
public sealed class RequestGuildMembershipHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCRequestGuildMembership;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCRequestGuildMembershipResponse
        {
            Result = CMsgClientToGCRequestGuildMembershipResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCRequestGuildMembershipResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
