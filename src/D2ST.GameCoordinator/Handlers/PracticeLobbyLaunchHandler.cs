using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Starts the lobby's game (7041). The lobby moves to SERVERSETUP and the
/// members see it, and the launch is answered with the generic result the
/// client's job waits for — a launch that is never answered never lets the
/// host's client start its local game server.
/// </summary>
public sealed class PracticeLobbyLaunchHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbyLaunchHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbyLaunch;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var launched = _lobbies.Launch(context);
        var response = new CMsgGenericResult { Eresult = launched ? 1u : 0u };

        return
        [
            new GcMessage(GcMsg.GCGenericResult, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
