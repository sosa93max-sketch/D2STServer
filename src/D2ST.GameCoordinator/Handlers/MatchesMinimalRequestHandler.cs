using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Matches;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Returns the compact scoreboard projection for requested match ids.
/// </summary>
public sealed class MatchesMinimalRequestHandler : IGcMessageHandler
{
    private readonly IMatchStore _matches;

    public MatchesMinimalRequestHandler(IMatchStore matches)
    {
        _matches = matches;
    }

    public uint MessageType => GcMsg.ClientToGCMatchesMinimalRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var minimal = context.Codec.Decode<CMsgClientToGCMatchesMinimalRequest>(request.Body);
        var records = _matches.GetMatchesMinimal(minimal.MatchIds ?? Array.Empty<ulong>());
        var response = new CMsgClientToGCMatchesMinimalResponse { LastMatch = true };

        foreach (var record in records)
        {
            var match = new CMsgDOTAMatchMinimal
            {
                MatchId = record.MatchId,
                StartTime = record.StartTime,
                Duration = record.DurationSeconds,
                GameMode = (DOTAGameMode)record.GameMode,
                MatchOutcome = OutcomeFor(record.WinningTeam),
                RadiantScore = record.RadiantScore,
                DireScore = record.DireScore,
                LobbyType = (uint)CSODOTALobby.LobbyType.Practice,
                IsPlayerDraft = false
            };

            match.Players.AddRange(record.Players.Select(player => new CMsgDOTAMatchMinimal.Player
            {
                AccountId = player.AccountId,
                HeroId = player.HeroId,
                Level = player.Level,
                Kills = player.Kills,
                Deaths = player.Deaths,
                Assists = player.Assists,
                PlayerSlot = player.PlayerSlot,
                Items = player.Items.ToArray()
            }));
            response.Matches.Add(match);
        }

        return
        [
            new GcMessage(GcMsg.ClientToGCMatchesMinimalResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }

    private static EMatchOutcome OutcomeFor(int winningTeam) => winningTeam switch
    {
        0 => EMatchOutcome.kEMatchOutcomeRadVictory,
        1 => EMatchOutcome.kEMatchOutcomeDireVictory,
        _ => EMatchOutcome.kEMatchOutcomeUnknown
    };
}
