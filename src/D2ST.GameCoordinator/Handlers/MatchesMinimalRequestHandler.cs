using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There are no recorded matches yet (8063 → 8064 with an empty list).
/// </summary>
public sealed class MatchesMinimalRequestHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCMatchesMinimalRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCMatchesMinimalResponse();
        return
        [
            new GcMessage(GcMsg.ClientToGCMatchesMinimalResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
