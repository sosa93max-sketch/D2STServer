using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Starts a ready check (8262 → 8263). The check itself is a field on the party
/// object, so the prompt reaches the other members as a party update.
/// </summary>
public sealed class PartyReadyCheckRequestHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public PartyReadyCheckRequestHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.PartyReadyCheckRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgPartyReadyCheckResponse { Result = _parties.StartReadyCheck(context) };

        return
        [
            new GcMessage(GcMsg.PartyReadyCheckResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
