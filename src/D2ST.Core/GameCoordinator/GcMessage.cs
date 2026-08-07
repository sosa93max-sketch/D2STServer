namespace D2ST.Core.GameCoordinator;

/// <summary>
/// A single Game Coordinator message on the wire: the numeric message type, the
/// protobuf-encoded body, and the GC job ids used to correlate a response with
/// the request that triggered it.
/// </summary>
/// <param name="MessageType">GC message id (e.g. 4006 = ClientHello).</param>
/// <param name="Body">protobuf-encoded message body.</param>
/// <param name="TargetJobId">Job id this message is replying to, if any.</param>
/// <param name="SourceJobId">Job id the sender assigned to this message, if any.</param>
public sealed record GcMessage(
    uint MessageType,
    byte[] Body,
    ulong? TargetJobId = null,
    ulong? SourceJobId = null);
