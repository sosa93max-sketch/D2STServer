using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Records where the game server is (4508). The address feeds the connect
/// string the lobby members dial; there is nothing to answer.
/// </summary>
public sealed class GameServerInfoHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public GameServerInfoHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.GCGameServerInfo;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var info = context.Codec.Decode<CMsgGameServerInfo>(request.Body);
        _lobbies.ServerInfo(context, info);
        return [];
    }
}
