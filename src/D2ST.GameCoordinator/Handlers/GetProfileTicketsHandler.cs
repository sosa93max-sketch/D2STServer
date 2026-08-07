using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The profile has no tickets (8073 → 8074 with an empty ticket list).
/// </summary>
public sealed class GetProfileTicketsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetProfileTickets;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var tickets = context.Codec.Decode<CMsgClientToGCGetProfileTickets>(request.Body);
        var response = new CMsgDOTAProfileTickets
        {
            Result = 1,
            AccountId = tickets.AccountId != 0 ? tickets.AccountId : context.AccountId
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCGetProfileTicketsResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
