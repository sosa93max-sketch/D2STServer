using D2ST.Core.GameCoordinator;
using D2ST.Core.Ranking;
using D2ST.GameCoordinator.Ranks;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the rank request (8879 → 8880) with the player's medal, computed
/// from their lobby MMR (Herald 1 by default).
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
        var info = RankMath.RankFor(rank.Mmr);
        var response = new CMsgGCToClientRankResponse
        {
            Result = CMsgGCToClientRankResponse.EResultCode.kSucceeded,
            RankValue = (uint)info.RankValue,
            RankData1 = (uint)Math.Max(0, rank.Mmr)
        };

        return
        [
            new GcMessage(GcMsg.GCToClientRankResponse, context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
