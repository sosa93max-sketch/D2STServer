using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Applies the host's lobby settings (7046). There is no reply: the settings are
/// fields of the lobby object, so the client redraws from the cache delta.
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
        _lobbies.SetDetails(context, details);
        return [];
    }
}
