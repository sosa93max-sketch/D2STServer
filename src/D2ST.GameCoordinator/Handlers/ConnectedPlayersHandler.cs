using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.GameCoordinator.Messaging;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The game server reports who is in the match (7034). The lobby service keeps
/// the Shared Object projection, while live scoreboard packets are forwarded
/// to every member so the Dota client can render kills, lead and buildings.
/// </summary>
public sealed class ConnectedPlayersHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;
    private readonly IGcMessageQueue _queue;

    public ConnectedPlayersHandler(LobbyService lobbies, IGcMessageQueue queue)
    {
        _lobbies = lobbies;
        _queue = queue;
    }

    public uint MessageType => GcMsg.GCConnectedPlayers;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var players = context.Codec.Decode<CMsgConnectedPlayers>(request.Body);
        var lobby = _lobbies.ConnectedPlayers(context, players);
        if (lobby is not null && HasClientVisibleState(players))
        {
            var update = new GcMessage(GcMsg.GCConnectedPlayers, request.Body);
            foreach (var member in lobby.AllMembers.Where(member =>
                         member.Id != 0 && member.Id != context.SteamId))
            {
                _queue.EnqueueToSteamId(member.Id, update);
            }
        }

        return [];
    }

    private static bool HasClientVisibleState(CMsgConnectedPlayers players) =>
        players.ConnectedPlayers.Count != 0
        || players.DisconnectedPlayers.Count != 0
        || players.PlayerDrafts.Count != 0
        || players.ShouldSerializeGameState()
        || players.ShouldSerializeFirstBloodHappened()
        || players.ShouldSerializeRadiantKills()
        || players.ShouldSerializeDireKills()
        || players.ShouldSerializeRadiantLead()
        || players.ShouldSerializeBuildingState();
}
