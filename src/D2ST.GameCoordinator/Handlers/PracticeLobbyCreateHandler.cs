using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Hosts a lobby (7038 → 7055). The lobby itself reaches the client as a
/// Shared Object cache it is subscribed to; the reply is the generic result the
/// modern client's create job waits for.
/// </summary>
public sealed class PracticeLobbyCreateHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbyCreateHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbyCreate;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var create = context.Codec.Decode<CMsgPracticeLobbyCreate>(request.Body);
        var created = _lobbies.Create(context, create);
        var response = new CMsgGenericResult
        {
            Eresult = created == DOTAJoinLobbyResult.DotaJoinResultSuccess ? 1u : 0u
        };

        return
        [
            new GcMessage(GcMsg.PracticeLobbyResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
