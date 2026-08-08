using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Matches;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Returns the aggregate hero standings available from recorded local-lobby
/// matches (7274 → 7275). Values are not presented as public Dota data.
/// </summary>
public sealed class GetHeroStandingsHandler : IGcMessageHandler
{
    private readonly IMatchStore _matches;

    public GetHeroStandingsHandler(IMatchStore matches)
    {
        _matches = matches;
    }

    public uint MessageType => GcMsg.GCGetHeroStandings;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgGCGetHeroStandingsResponse();
        foreach (var stats in _matches.GetHeroStandings(context.AccountId))
        {
            response.Standings.Add(new CMsgGCGetHeroStandingsResponse.Hero
            {
                HeroId = stats.HeroId,
                Wins = ToUInt(stats.Wins),
                Losses = ToUInt(stats.Losses),
                AvgKills = Average(stats.TotalKills, stats.Games),
                AvgDeaths = Average(stats.TotalDeaths, stats.Games),
                AvgAssists = Average(stats.TotalAssists, stats.Games),
                AvgGpm = Average(stats.TotalGoldPerMin, stats.Games),
                AvgXpm = Average(stats.TotalXpPerMinute, stats.Games),
                AvgLasthits = Average(stats.TotalLastHits, stats.Games),
                AvgDenies = Average(stats.TotalDenies, stats.Games)
            });
        }

        return
        [
            new GcMessage(GcMsg.GCGetHeroStandingsResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }

    private static float Average(long total, int games) =>
        games <= 0 ? 0f : (float)total / games;

    private static uint ToUInt(int value) => value <= 0 ? 0u : (uint)value;
}
