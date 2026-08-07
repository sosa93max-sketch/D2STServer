using D2ST.Core.Accounts;
using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Ranks;
using D2ST.GameCoordinator.Lobbies;
using D2ST.GameCoordinator.Messaging;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The game server reports the match ended (7004). Every lobby member on the
/// winning team gains MMR, every member on the losing team loses it; the lobby
/// moves to POSTGAME and the players get the match-signed-out push (8081).
/// </summary>
public sealed class GameMatchSignOutHandler : IGcMessageHandler
{
    private readonly LobbyService _lobbies;
    private readonly IRankStore _ranks;
    private readonly IGcMessageQueue _queue;

    public GameMatchSignOutHandler(LobbyService lobbies, IRankStore ranks, IGcMessageQueue queue)
    {
        _lobbies = lobbies;
        _ranks = ranks;
        _queue = queue;
    }

    public uint MessageType => GcMsg.GameMatchSignOut;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var signOut = context.Codec.Decode<CMsgGameMatchSignOut>(request.Body);
        var lobby = _lobbies.FindByServer(context.SteamId);
        var matchId = signOut.MatchId != 0 ? signOut.MatchId : lobby?.MatchId ?? 0;

        if (lobby is not null)
        {
            var winner = signOut.GoodGuysWin
                ? DotaGcTeam.DotaGcTeamGoodGuys
                : DotaGcTeam.DotaGcTeamBadGuys;
            var results = lobby.AllMembers
                .Select(member => (SteamAccount.AccountIdFromSteamId(member.Id), member.Team == winner))
                .ToList();

            _ranks.ApplyMatchResult(results);

            lobby.state = CSODOTALobby.State.Postgame;
            _lobbies.Publish(lobby);

            var pushed = context.Codec.Encode(new CMsgGCToClientMatchSignedOut { MatchId = matchId });
            foreach (var member in lobby.AllMembers)
            {
                _queue.EnqueueToSteamId(member.Id, new GcMessage(GcMsg.GCToClientMatchSignedOut, pushed));
            }
        }

        var response = context.Codec.Encode(new CMsgGameMatchSignoutResponse { MatchId = matchId });
        return
        [
            new GcMessage(GcMsg.GameMatchSignOutResponse, response, TargetJobId: request.SourceJobId)
        ];
    }
}
