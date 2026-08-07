using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Accepts or declines a party invite (4503). There is no reply: the client
/// learns the outcome from the caches — the invite is unsubscribed, and an
/// acceptance subscribes it to the party.
/// </summary>
public sealed class PartyInviteResponseHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public PartyInviteResponseHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.PartyInviteResponse;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _parties.RespondToInvite(context, context.Codec.Decode<CMsgPartyInviteResponse>(request.Body));
        return [];
    }
}
