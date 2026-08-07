using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Guilds are not implemented; the request (8673 → 8674) answers success with
/// no guild data.
/// </summary>
public sealed class RequestGuildDataHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCRequestGuildData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCRequestGuildDataResponse
        {
            Result = CMsgClientToGCRequestGuildDataResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCRequestGuildDataResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
