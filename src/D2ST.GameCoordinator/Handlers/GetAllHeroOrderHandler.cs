using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Matches;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Returns the deterministic order of heroes that have appeared in persisted
/// local-lobby match data (7606 → 7607).
/// </summary>
public sealed class GetAllHeroOrderHandler : IGcMessageHandler
{
    private readonly IMatchStore _matches;

    public GetAllHeroOrderHandler(IMatchStore matches)
    {
        _matches = matches;
    }

    public uint MessageType => GcMsg.ClientToGCGetAllHeroOrder;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCGetAllHeroOrderResponse
        {
            HeroIds = _matches.GetHeroOrder().ToArray()
        };
        return
        [
            new GcMessage(GcMsg.ClientToGCGetAllHeroOrderResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
