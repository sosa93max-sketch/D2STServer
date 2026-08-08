using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Profiles;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the lightweight profile-card stats request used by the mini-profile
/// flow. The wire response is the bare <see cref="CMsgDOTAProfileCard"/>.
/// </summary>
public sealed class GetProfileCardStatsHandler : IGcMessageHandler
{
    private readonly ProfileCardBuilder _cards;

    public GetProfileCardStatsHandler(ProfileCardBuilder cards)
    {
        _cards = cards;
    }

    public uint MessageType => GcMsg.ClientToGCGetProfileCardStats;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var card = _cards.Build(context.AccountId);

        return
        [
            new GcMessage(
                GcMsg.ClientToGCGetProfileCardStatsResponse,
                context.Codec.Encode(card),
                TargetJobId: request.SourceJobId)
        ];
    }
}
