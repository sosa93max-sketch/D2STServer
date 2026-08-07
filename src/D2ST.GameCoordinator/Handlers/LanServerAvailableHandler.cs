using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// A local listen server became reachable (4511). The lobby starts carrying a
/// connect string, still in SERVERSETUP.
/// </summary>
public sealed class LanServerAvailableHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public LanServerAvailableHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.GCLANServerAvailable;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var available = context.Codec.Decode<CMsgLANServerAvailable>(request.Body);
        _lobbies.LanServerAvailable(context, available);
        return [];
    }
}
