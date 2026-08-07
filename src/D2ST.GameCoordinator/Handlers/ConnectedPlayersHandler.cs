using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The game server reports who is in the match (7034). Hero picks, leaver
/// states and the game state are mirrored onto the lobby object.
/// </summary>
public sealed class ConnectedPlayersHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public ConnectedPlayersHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.GCConnectedPlayers;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var players = context.Codec.Decode<CMsgConnectedPlayers>(request.Body);
        _lobbies.ConnectedPlayers(context, players);
        return [];
    }
}
