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

        // Nothing here tracks badges, ranks or showcase slots yet; the card is
        // the identity only, which is enough for the client to render it.
        var rank = _ranks.GetOrCreate(accountId);
        var info = RankMath.RankFor(rank.Mmr);
        var card = new CMsgDOTAProfileCard
        {
            AccountId = accountId,
            BadgePoints = 0,
            EventId = 0,
            RankTier = (uint)info.Tier,
            // The client draws the medal from the score; without it the profile
            // shows the account as uncalibrated even when a tier is set.
            RankTierScore = (uint)Math.Max(0, rank.Mmr),
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
