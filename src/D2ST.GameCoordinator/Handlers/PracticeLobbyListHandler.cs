using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Lists the public lobbies the browser shows (7042 → 7043), filtered by the
/// region and game mode the request asks for.
/// </summary>
public sealed class PracticeLobbyListHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbyListHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbyList;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var query = context.Codec.Decode<CMsgPracticeLobbyList>(request.Body);
        var response = new CMsgPracticeLobbyListResponse();
        response.Lobbies.AddRange(_lobbies.List(query));

        return
        [
            new GcMessage(GcMsg.PracticeLobbyListResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
