using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Profiles;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Serves the profile card shown on a player's Dota profile. The response
/// message id carries a bare <see cref="CMsgDOTAProfileCard"/>: there is no
/// wrapper response type in the protocol.
/// </summary>
public sealed class GetProfileCardHandler : IGcMessageHandler
{
    private readonly ProfileCardBuilder _cards;

    public GetProfileCardHandler(ProfileCardBuilder cards)
    {
        _cards = cards;
    }

    public uint MessageType => GcMsg.ClientToGCGetProfileCard;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var requested = context.Codec.Decode<CMsgClientToGCGetProfileCard>(request.Body);
        var accountId = requested.AccountId != 0 ? requested.AccountId : context.AccountId;
        var card = _cards.Build(accountId);

        return
        [
            new GcMessage(
                GcMsg.ClientToGCGetProfileCardResponse,
                context.Codec.Encode(card),
                TargetJobId: request.SourceJobId)
        ];
    }
}
