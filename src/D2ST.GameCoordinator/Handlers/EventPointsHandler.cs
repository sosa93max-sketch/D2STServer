using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Reports the battle pass / event points of an account. Nothing awards points
/// yet, so every total is zero, but the requested event id is echoed back
/// because the client matches the response to the event it asked about.
/// </summary>
public sealed class EventPointsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.GetEventPoints;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var requested = context.Codec.Decode<CMsgDOTAGetEventPoints>(request.Body);
        var response = new CMsgDOTAGetEventPointsResponse
        {
            AccountId = requested.AccountId != 0 ? requested.AccountId : context.AccountId,
            EventId = requested.EventId,
            TotalPoints = 0,
            TotalPremiumPoints = 0,
            Points = 0,
            PremiumPoints = 0,
            Owned = false
        };

        return
        [
            new GcMessage(
                GcMsg.GetEventPointsResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
