using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Matches;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Returns aggregates for players who shared a team with the current account.
/// </summary>
public sealed class TeammateStatsHandler : IGcMessageHandler
{
    private readonly IMatchStore _matches;

    public TeammateStatsHandler(IMatchStore matches)
    {
        _matches = matches;
    }

    public uint MessageType => GcMsg.ClientToGCTeammateStatsRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCTeammateStatsResponse { Success = true };
        response.TeammateStats.AddRange(_matches.GetTeammateStats(context.AccountId)
            .Select(stat => new CMsgClientToGCTeammateStatsResponse.TeammateStat
            {
                AccountId = stat.AccountId,
                Games = stat.Games,
                Wins = stat.Wins,
                MostRecentGameTimestamp = stat.MostRecentGameTimestamp,
                MostRecentGameMatchId = stat.MostRecentGameMatchId,
                Performance = stat.Performance
            }));

        return
        [
            new GcMessage(GcMsg.ClientToGCTeammateStatsResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
