using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Matches;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Reads the persisted local-lobby history for the requested account.
/// </summary>
public sealed class GetPlayerMatchHistoryHandler : IGcMessageHandler
{
    private readonly IMatchStore _matches;

    public GetPlayerMatchHistoryHandler(IMatchStore matches)
    {
        _matches = matches;
    }

    public uint MessageType => GcMsg.DOTAGetPlayerMatchHistory;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var history = context.Codec.Decode<CMsgDOTAGetPlayerMatchHistory>(request.Body);
        var accountId = history.AccountId != 0 ? history.AccountId : context.AccountId;
        var includePracticeMatches = !history.ShouldSerializeIncludePracticeMatches()
            || history.IncludePracticeMatches;
        var entries = _matches.GetPlayerMatchHistory(
            accountId,
            history.StartAtMatchId,
            history.MatchesRequested,
            history.HeroId,
            includePracticeMatches,
            history.IncludeCustomGames,
            history.IncludeEventGames);
        var response = new CMsgDOTAGetPlayerMatchHistoryResponse
        {
            RequestId = history.RequestId
        };

        response.Matches.AddRange(entries.Select(entry => new CMsgDOTAGetPlayerMatchHistoryResponse.Match
        {
            MatchId = entry.MatchId,
            StartTime = entry.StartTime,
            HeroId = entry.HeroId,
            Winner = entry.Winner,
            GameMode = entry.GameMode,
            LobbyType = (uint)CSODOTALobby.LobbyType.Practice,
            Abandon = entry.Abandon,
            Duration = entry.DurationSeconds
        }));

        return
        [
            new GcMessage(GcMsg.DOTAGetPlayerMatchHistoryResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
