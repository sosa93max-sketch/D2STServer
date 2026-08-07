using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Reports the pro teams the caller belongs to. Teams are not modelled here, so
/// the list is empty; the client needs the reply to finish loading the profile
/// page regardless.
/// </summary>
public sealed class MyTeamInfoHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.MyTeamInfoRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request) =>
    [
        new GcMessage(
            GcMsg.ToClientTeamsInfo,
            context.Codec.Encode(new CMsgDOTATeamsInfo()),
            TargetJobId: request.SourceJobId)
    ];
}
