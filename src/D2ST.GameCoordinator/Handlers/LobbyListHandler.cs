using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The build-6783 lobby browser asks with 8011 instead of the legacy 7042; the
/// request and the entries are the same shape, so the reply reuses the lobby
/// list service.
/// </summary>
public sealed class LobbyListHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public LobbyListHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.GCLobbyList;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var list = context.Codec.Decode<CMsgLobbyList>(request.Body);
        var entries = _lobbies.List(new CMsgPracticeLobbyList
        {
            Region = list.ServerRegion,
            GameMode = list.GameMode
        });

        var response = new CMsgLobbyListResponse();
        response.Lobbies.AddRange(entries);

        return
        [
            new GcMessage(GcMsg.GCLobbyListResponse, context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
