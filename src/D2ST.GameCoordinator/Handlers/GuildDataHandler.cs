using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the legacy guild query with "you have no guild invitation". Guilds
/// are not modelled here; the reply exists so the client stops holding the job
/// open on every main-menu load.
/// </summary>
public sealed class GuildDataHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.RequestGuildData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request) =>
    [
        new GcMessage(
            GcMsg.GuildData,
            context.Codec.Encode(new CMsgDOTAGuildInviteData { InvitedToGuild = false }),
            TargetJobId: request.SourceJobId)
    ];
}
