using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers GCClientHello with the three-packet welcome sequence Dota expects:
/// out-of-logon-queue status, the welcome payload (version, game data and the
/// subscribed SO caches), then a have-session status.
/// </summary>
public sealed class ClientHelloHandler : IGcMessageHandler
{
    private readonly WelcomeBuilder _welcome;
    private readonly IEnumerable<IGcLogonListener> _logonListeners;

    public ClientHelloHandler(WelcomeBuilder welcome, IEnumerable<IGcLogonListener> logonListeners)
    {
        _welcome = welcome;
        _logonListeners = logonListeners;
    }

    public uint MessageType => GcMsg.ClientHello;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var hello = context.Codec.Decode<CMsgClientHello>(request.Body);
        if (hello.Version != 0)
        {
            context.ClientVersion = (int)hello.Version;
        }

        var connecting = context.Codec.Encode(new CMsgConnectionStatus
        {
            Status = GCConnectionStatus.GCConnectionStatusNOSESSIONINLOGONQUEUE
        });

        var welcome = context.Codec.Encode(_welcome.Build(context));

        var connected = context.Codec.Encode(new CMsgConnectionStatus
        {
            Status = GCConnectionStatus.GCConnectionStatusHAVESESSION
        });

        // Anything a client is handed without asking (the default chat
        // channels) is pushed after the welcome, never before: the session the
        // welcome opens is what makes the client able to read it at all.
        foreach (var listener in _logonListeners)
        {
            listener.OnLogon(context);
        }

        return new[]
        {
            new GcMessage(GcMsg.ClientConnectionStatus, connecting),
            new GcMessage(GcMsg.ClientWelcome, welcome, TargetJobId: request.SourceJobId),
            new GcMessage(GcMsg.ClientConnectionStatus, connected)
        };
    }
}
