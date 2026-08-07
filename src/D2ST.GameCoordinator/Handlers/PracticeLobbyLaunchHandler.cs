using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Starts the lobby's game (7041). The lobby moves to SERVERSETUP and the
/// members see it; there is no game server to hand it to yet.
/// </summary>
public sealed class PracticeLobbyLaunchHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbyLaunchHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbyLaunch;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _lobbies.Launch(context);
        return [];
    }
}
