using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Removes a member from the caller's party (4504). Leader only.</summary>
public sealed class KickFromPartyHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public KickFromPartyHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.KickFromParty;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var kick = context.Codec.Decode<CMsgKickFromParty>(request.Body);
        _parties.Kick(context, kick.SteamId);
        return [];
    }
}
