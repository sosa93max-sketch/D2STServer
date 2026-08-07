using D2ST.Core.GameCoordinator;

namespace D2ST.GameCoordinator;

/// <summary>Handles one GC message type and returns the messages to send back.</summary>
public interface IGcMessageHandler
{
    uint MessageType { get; }

    IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request);
}
