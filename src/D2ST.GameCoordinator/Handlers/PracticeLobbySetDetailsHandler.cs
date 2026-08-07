using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Applies the host's lobby settings (7046 → 7055 with the generic result the
/// modern client waits for; the settings are fields of the lobby object, so the
/// client redraws from the cache delta).
/// </summary>
public sealed class PracticeLobbySetDetailsHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbySetDetailsHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbySetDetails;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var details = context.Codec.Decode<CMsgPracticeLobbySetDetails>(request.Body);
        var applied = _lobbies.SetDetails(context, details);
        var response = new CMsgGenericResult { Eresult = applied ? 1u : 0u };

        return
        [
            new GcMessage(GcMsg.PracticeLobbyResponse, context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
