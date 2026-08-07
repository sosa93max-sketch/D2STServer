using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Joins a lobby (7044 → 7113). A wrong pass key, a lobby that is gone or one
/// whose game already started are refused with the result the client renders.
/// </summary>
public sealed class PracticeLobbyJoinHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbyJoinHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbyJoin;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var join = context.Codec.Decode<CMsgPracticeLobbyJoin>(request.Body);
        var response = new CMsgPracticeLobbyJoinResponse { Result = _lobbies.Join(context, join) };

        return
        [
            new GcMessage(GcMsg.PracticeLobbyJoinResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
