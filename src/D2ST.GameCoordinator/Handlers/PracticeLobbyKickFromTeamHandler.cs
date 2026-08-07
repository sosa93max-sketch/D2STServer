using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Lobbies;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Moves a player out of its team and into the player pool (8047), leaving it in
/// the lobby. Host only.
/// </summary>
public sealed class PracticeLobbyKickFromTeamHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;

    public PracticeLobbyKickFromTeamHandler(LobbyService lobbies)
    {
        _lobbies = lobbies;
    }

    public uint MessageType => GcMsg.PracticeLobbyKickFromTeam;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var kick = context.Codec.Decode<CMsgPracticeLobbyKickFromTeam>(request.Body);
        _lobbies.KickFromTeam(context, kick.AccountId);
        return [];
    }
}
