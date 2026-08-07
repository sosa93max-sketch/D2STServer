using D2ST.Core.GameCoordinator;
using D2ST.Core.Ranking;
using D2ST.GameCoordinator.Ranks;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Serves the profile card shown on a player's Dota profile. The response
/// message id carries a bare <see cref="CMsgDOTAProfileCard"/>: there is no
/// wrapper response type in the protocol.
/// </summary>
public sealed class GetProfileCardHandler : IGcMessageHandler
{
    private readonly IRankStore _ranks;

    public GetProfileCardHandler(IRankStore ranks)
    {
        _ranks = ranks;
    }

    public uint MessageType => GcMsg.ClientToGCGetProfileCard;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var requested = context.Codec.Decode<CMsgClientToGCGetProfileCard>(request.Body);
        var accountId = requested.AccountId != 0 ? requested.AccountId : context.AccountId;

        // Badge/showcase data is not persisted yet; rank data is backed by the
        // rank store because the client renders it directly from this card.
        var rank = _ranks.GetOrCreate(accountId);
        var info = RankMath.VisibleRankFor(rank);
        var card = new CMsgDOTAProfileCard
        {
            AccountId = accountId,
            BadgePoints = 0,
            EventId = 0,
            // rank_tier is the encoded medal (for example Divine 3 = 73),
            // not just the base tier (7).
            RankTier = (uint)info.RankValue,
            // The client renders this field as "{value}%". It is progress
            // through the current star, never the player's raw MMR.
            RankTierScore = (uint)info.ProgressPercent,
            LeaderboardRank = 0,
            IsPlusSubscriber = false
        };

        return
        [
            new GcMessage(
                GcMsg.ClientToGCGetProfileCardResponse,
                context.Codec.Encode(card),
                TargetJobId: request.SourceJobId)
        ];
    }
}
