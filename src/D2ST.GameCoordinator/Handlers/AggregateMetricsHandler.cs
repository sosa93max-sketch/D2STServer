using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Client telemetry (4523). The real GC accepts it and never answers; handling
/// it keeps the diagnostics dump clean.
/// </summary>
public sealed class AggregateMetricsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCAggregateMetrics;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request) => [];
}
