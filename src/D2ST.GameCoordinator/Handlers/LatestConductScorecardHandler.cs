using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Matches;
using D2ST.GameCoordinator.Profiles;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the conduct request (8095) explicitly. An omitted or zero behavior
/// score is interpreted by the client as restricted; local deployments have no
/// report pipeline yet, so the scorecard uses the local neutral policy and real
/// persisted match/abandon counts.
/// </summary>
public sealed class LatestConductScorecardHandler : IGcMessageHandler
{
    private readonly IMatchStore _matches;

    public LatestConductScorecardHandler(IMatchStore matches)
    {
        _matches = matches;
    }

    public uint MessageType => GcMsg.ClientToGCLatestConductScorecardRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var stats = _matches.GetProfileStats(context.AccountId);
        var scorecard = LocalConductState.BuildScorecard(context.AccountId, stats);

        return
        [
            new GcMessage(
                GcMsg.ClientToGCLatestConductScorecard,
                context.Codec.Encode(scorecard),
                TargetJobId: request.SourceJobId)
        ];
    }
}
