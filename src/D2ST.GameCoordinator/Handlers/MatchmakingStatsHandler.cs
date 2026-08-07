using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the matchmaking population request the client fires as soon as the
/// main menu opens. The per-matchgroup player counts are the numbers shown next
/// to the region/mode pickers; there is no matchmaking here yet, so the reply
/// is an empty population rather than silence (the client retries forever while
/// the job is outstanding).
/// </summary>
public sealed class MatchmakingStatsHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.MatchmakingStatsRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request) =>
    [
        new GcMessage(
            GcMsg.MatchmakingStatsResponse,
            context.Codec.Encode(new CMsgDOTAMatchmakingStatsResponse { MatchgroupsVersion = 0 }),
            TargetJobId: request.SourceJobId)
    ];
}
