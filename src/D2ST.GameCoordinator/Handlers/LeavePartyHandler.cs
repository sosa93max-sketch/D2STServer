using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Leaves the caller's party (4505); the remaining members see the update.</summary>
public sealed class LeavePartyHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public LeavePartyHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.LeaveParty;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _parties.Leave(context);
        return [];
    }
}
