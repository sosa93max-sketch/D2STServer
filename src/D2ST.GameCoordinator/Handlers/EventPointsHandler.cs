using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Reports local Dota Plus shards through the event-points envelope used by
/// older client builds. Other battle-pass event ids remain empty.
/// </summary>
public sealed class EventPointsHandler : IGcMessageHandler
{
    private readonly IDotaPlusStore _plus;

    public EventPointsHandler(IDotaPlusStore plus)
    {
        _plus = plus;
    }

    public uint MessageType => GcMsg.GetEventPoints;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var requested = context.Codec.Decode<CMsgDOTAGetEventPoints>(request.Body);
        var accountId = requested.AccountId != 0 ? requested.AccountId : context.AccountId;
        var isPlus = requested.EventId == (uint)EEvent.EventIdPlusSubscription;
        var snapshot = isPlus ? _plus.GetSnapshot(accountId) : null;
        var points = snapshot is null ? 0u : ToUInt(snapshot.Shards);
        var response = new CMsgDOTAGetEventPointsResponse
        {
            AccountId = accountId,
            EventId = requested.EventId,
            TotalPoints = points,
            TotalPremiumPoints = points,
            Points = points,
            PremiumPoints = 0,
            Owned = snapshot?.Active ?? false
        };

        return
        [
            new GcMessage(
                GcMsg.GetEventPointsResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }

    private static uint ToUInt(long value) =>
        value <= 0 ? 0u : value >= uint.MaxValue ? uint.MaxValue : (uint)value;
}
