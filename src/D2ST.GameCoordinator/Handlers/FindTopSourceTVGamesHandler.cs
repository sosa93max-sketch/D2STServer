using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// There are no live Source TV broadcasts (8009 → 8010 with an empty list).
/// </summary>
public sealed class FindTopSourceTVGamesHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCFindTopSourceTVGames;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgGCToClientFindTopSourceTVGamesResponse();
        return
        [
            new GcMessage(GcMsg.GCToClientFindTopSourceTVGamesResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
