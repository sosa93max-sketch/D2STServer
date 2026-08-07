using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Invites a player to the caller's party (4501 → 4502). The reply only says
/// which group the invite belongs to and whether the target could be reached;
/// the invite itself travels to the target as a Shared Object cache.
/// </summary>
public sealed class InviteToPartyHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public InviteToPartyHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.InviteToParty;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var invite = context.Codec.Decode<CMsgInviteToParty>(request.Body);
        var created = _parties.Invite(context, invite);

        return
        [
            new GcMessage(GcMsg.InvitationCreated, context.Codec.Encode(created), TargetJobId: request.SourceJobId)
        ];
    }
}
