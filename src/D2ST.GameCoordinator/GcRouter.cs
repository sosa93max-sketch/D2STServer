using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Diagnostics;
using D2ST.Protocol.Dota;
using Microsoft.Extensions.Logging;

namespace D2ST.GameCoordinator;

/// <summary>
/// Dispatches an inbound GC message to its registered handler. Unhandled message
/// types are logged (this is the signal used to discover what a new/old build
/// expects) and produce no response.
/// </summary>
public sealed class GcRouter
{
    private readonly IReadOnlyDictionary<uint, IGcMessageHandler> _handlers;
    private readonly ILogger<GcRouter> _logger;
    private readonly IGcMessageRecorder _recorder;

    public GcRouter(IEnumerable<IGcMessageHandler> handlers, ILogger<GcRouter> logger, IGcMessageRecorder? recorder = null)
    {
        _handlers = handlers.ToDictionary(handler => handler.MessageType);
        _logger = logger;
        _recorder = recorder ?? NullGcMessageRecorder.Instance;
    }

    public bool CanHandle(uint messageType) => _handlers.ContainsKey(messageType);

    public IReadOnlyList<GcMessage> Dispatch(GcContext context, GcMessage request)
    {
        if (_handlers.TryGetValue(request.MessageType, out var handler))
        {
            return handler.Handle(context, request);
        }

        _logger.LogWarning(
            "Unhandled GC message {MessageType} ({MessageName}) from account {AccountId} (build {ClientVersion}, job {SourceJobId}, {BodyLength} body bytes)",
            request.MessageType,
            GcMsgNames.Describe(request.MessageType),
            context.AccountId,
            context.ClientVersion,
            request.SourceJobId,
            request.Body?.Length ?? 0);
        _recorder.RecordUnhandled(context, request);
        return Array.Empty<GcMessage>();
    }
}
