using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Reports that no weekend tourney (Battle Cup) is scheduled. An empty division
/// list is how the GC says "nothing is running", which the client renders as an
/// unavailable Battle Cup instead of a request that never completes.
/// </summary>
public sealed class WeekendTourneyScheduleHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.GetWeekendTourneySchedule;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request) =>
    [
        new GcMessage(
            GcMsg.WeekendTourneySchedule,
            context.Codec.Encode(new CMsgWeekendTourneySchedule()),
            TargetJobId: request.SourceJobId)
    ];
}
