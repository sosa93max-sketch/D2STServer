using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Withdraws invites the caller's party sent (7589). An empty request cancels
/// all of them, which is what the client sends when the party starts a match.
/// </summary>
public sealed class CancelPartyInvitesHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public CancelPartyInvitesHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.CancelPartyInvites;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var cancel = context.Codec.Decode<CMsgDOTACancelGroupInvites>(request.Body);
        _parties.CancelInvites(context, cancel.InvitedSteamids ?? []);
        return [];
    }
}
