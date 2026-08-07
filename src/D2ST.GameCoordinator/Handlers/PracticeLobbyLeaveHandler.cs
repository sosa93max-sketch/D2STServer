using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Leaves the caller's lobby (7040); the remaining members see the update.</summary>
public sealed class PracticeLobbyLeaveHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbyLeaveHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbyLeave;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _lobbies.Leave(context);
        return [];
    }
}
