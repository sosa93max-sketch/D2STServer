using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No matches have been recorded (7408 → 7409 with an empty list).
/// </summary>
public sealed class GetPlayerMatchHistoryHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.DOTAGetPlayerMatchHistory;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgDOTAGetPlayerMatchHistoryResponse();
        return
        [
            new GcMessage(GcMsg.DOTAGetPlayerMatchHistoryResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
