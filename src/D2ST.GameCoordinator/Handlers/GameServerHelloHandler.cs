using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers a game server's GC hello (4007) with the server welcome (4005).
/// Without it the listen server a launched lobby starts never finishes its GC
/// connection, which is what used to take the whole client down on "Start".
/// </summary>
public sealed class GameServerHelloHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ServerHello;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        if (request.Body.Length > 0)
        {
            var hello = context.Codec.Decode<CMsgClientHello>(request.Body);
            if (hello.Version != 0)
            {
                context.ClientVersion = (int)hello.Version;
            }
        }

        var welcome = context.Codec.Encode(new CMsgClientWelcome
        {
            Version = (uint)context.ClientVersion,
            GcSocacheFileVersion = (uint)context.Profile.SocacheFileVersion
        });

        return
        [
            new GcMessage(GcMsg.ServerWelcome, welcome, TargetJobId: request.SourceJobId)
        ];
    }
}
