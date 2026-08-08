using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Refreshes the local weekly challenge projection on client request.</summary>
public sealed class DotaPlusWeeklyChallengeResultHandler : IGcMessageHandler
{
    private readonly DotaPlusProjection _projection;

    public DotaPlusWeeklyChallengeResultHandler(DotaPlusProjection projection)
    {
        _projection = projection;
    }

    public uint MessageType => GcMsg.ClientToGCRequestPlusWeeklyChallengeResult;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _ = context.Codec.Decode<CMsgClientToGCRequestPlusWeeklyChallengeResult>(request.Body);
        _projection.RefreshChallenges(context.AccountId);
        return
        [
            new GcMessage(
                GcMsg.ClientToGCRequestPlusWeeklyChallengeResultResponse,
                context.Codec.Encode(new CMsgClientToGCRequestPlusWeeklyChallengeResultResponse()),
                TargetJobId: request.SourceJobId)
        ];
    }
}
