using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Marks the caller as the party's coach or as a player again (7343). The flag
/// lives on the member entry, so every other client redraws the slot.
/// </summary>
public sealed class PartyMemberSetCoachHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public PartyMemberSetCoachHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.PartyMemberSetCoach;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var setCoach = context.Codec.Decode<CMsgDOTAPartyMemberSetCoach>(request.Body);
        _parties.SetCoach(context, setCoach.WantsCoach);
        return [];
    }
}
