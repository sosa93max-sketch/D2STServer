using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The game server is up and the match is running (4506): the lobby moves to
/// RUN with a match id, and every member learns where to connect.
/// </summary>
public sealed class ServerAvailableHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public ServerAvailableHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.GCServerAvailable;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _lobbies.ServerAvailable(context);
        return [];
    }
}
