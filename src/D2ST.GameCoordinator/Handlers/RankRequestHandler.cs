using D2ST.Core.GameCoordinator;
using D2ST.Core.Ranking;
using D2ST.GameCoordinator.Ranks;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the rank request (8879 → 8880) with the player's encoded medal,
/// MMR and progress percentage. Uncalibrated accounts use rank value zero.
/// </summary>
public sealed class RankRequestHandler : IGcMessageHandler
{
    private readonly IRankStore _ranks;

    public RankRequestHandler(IRankStore ranks)
    {
        _ranks = ranks;
    }

    public uint MessageType => GcMsg.ClientToGCRankRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var rank = _ranks.GetOrCreate(context.AccountId);
        var info = RankMath.VisibleRankFor(rank);
        var response = new CMsgGCToClientRankResponse
        {
            Result = CMsgGCToClientRankResponse.EResultCode.kSucceeded,
            RankValue = (uint)info.RankValue,
            RankData1 = info.RankValue == 0 ? 0u : (uint)Math.Max(0, rank.Mmr),
            RankData2 = (uint)info.ProgressPercent,
            RankData3 = 0
        };

        return
        [
            new GcMessage(GcMsg.GCToClientRankResponse, context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
