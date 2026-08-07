using D2ST.Core.GameCoordinator;

namespace D2ST.GameCoordinator.Diagnostics;

/// <summary>Sink for GC messages the router could not dispatch.</summary>
public interface IGcMessageRecorder
{
    void RecordUnhandled(GcContext context, GcMessage message);
}

/// <summary>Recorder used when the dump is disabled (tests, production opt-out).</summary>
public sealed class NullGcMessageRecorder : IGcMessageRecorder
{
    public static readonly NullGcMessageRecorder Instance = new();

    public void RecordUnhandled(GcContext context, GcMessage message)
    {
    }
}
