using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// A player failed to load (7088). The launch aborts: the lobby returns to the
/// UI state so the host can start over.
/// </summary>
public sealed class PlayerFailedToConnectHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PlayerFailedToConnectHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.GCPlayerFailedToConnect;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var failed = context.Codec.Decode<CMsgDOTAPlayerFailedToConnect>(request.Body);
        _lobbies.PlayerFailedToConnect(context, failed);
        return [];
    }
}
