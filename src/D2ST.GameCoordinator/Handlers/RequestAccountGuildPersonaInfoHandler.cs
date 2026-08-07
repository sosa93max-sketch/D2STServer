using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No guilds exist, so persona info is empty (8727 → 8728, success).
/// </summary>
public sealed class RequestAccountGuildPersonaInfoHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCRequestAccountGuildPersonaInfo;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCRequestAccountGuildPersonaInfoResponse
        {
            Result = CMsgClientToGCRequestAccountGuildPersonaInfoResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCRequestAccountGuildPersonaInfoResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
