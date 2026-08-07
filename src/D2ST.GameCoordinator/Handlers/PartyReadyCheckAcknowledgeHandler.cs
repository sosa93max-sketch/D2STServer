using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Parties;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Answers a running ready check (8264): the party object carries the tally.</summary>
public sealed class PartyReadyCheckAcknowledgeHandler : IGcMessageHandler
{
    private readonly PartyService _parties;

    public PartyReadyCheckAcknowledgeHandler(PartyService parties)
    {
        _parties = parties;
    }

    public uint MessageType => GcMsg.PartyReadyCheckAcknowledge;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var acknowledge = context.Codec.Decode<CMsgPartyReadyCheckAcknowledge>(request.Body);
        _parties.AcknowledgeReadyCheck(context, acknowledge.ReadyStatus);
        return [];
    }
}
