using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the GC keepalive. The client drops its GC session (and re-runs the
/// hello handshake) when a ping goes unanswered, so this has to reply even
/// though both bodies are empty.
/// </summary>
public sealed class PingHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.PingRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request) =>
    [
        new GcMessage(
            GcMsg.PingResponse,
            context.Codec.Encode(new CMsgGCClientPing()),
            TargetJobId: request.SourceJobId)
    ];
}
