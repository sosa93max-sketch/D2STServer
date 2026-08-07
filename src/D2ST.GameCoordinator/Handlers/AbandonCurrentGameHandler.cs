using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The cancel button of the launch overlay (7035): aborts a launch in progress
/// back to the lobby UI, or leaves the lobby when nothing is launching. No
/// reply, like the real GC.
/// </summary>
public sealed class AbandonCurrentGameHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public AbandonCurrentGameHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.AbandonCurrentGame;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _lobbies.AbandonCurrentGame(context);
        return [];
    }
}
