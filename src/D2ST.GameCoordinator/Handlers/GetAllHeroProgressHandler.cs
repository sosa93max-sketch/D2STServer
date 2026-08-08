using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Matches;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Projects persisted per-account hero rows into the existing all-hero
/// progress contract (7521 → 7522). Challenge timing/lap history is not
/// persisted yet and is therefore left unset.
/// </summary>
public sealed class GetAllHeroProgressHandler : IGcMessageHandler
{
    private readonly IMatchStore _matches;

    public GetAllHeroProgressHandler(IMatchStore matches)
    {
        _matches = matches;
    }

    public uint MessageType => GcMsg.ClientToGCGetAllHeroProgress;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var progress = context.Codec.Decode<CMsgClientToGCGetAllHeroProgress>(request.Body);
        var accountId = progress.AccountId != 0 ? progress.AccountId : context.AccountId;
        var heroStats = _matches.GetHeroStats(accountId);
        var currentHero = heroStats.FirstOrDefault();
        var response = new CMsgClientToGCGetAllHeroProgressResponse
        {
            AccountId = accountId,
            CurrHeroId = currentHero?.HeroId ?? 0,
            CurrHeroGames = ToUInt(currentHero?.Games ?? 0),
            LapHeroesCompleted = ToUInt(heroStats.Count(stats => stats.Games > 0))
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCGetAllHeroProgressResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }

    private static uint ToUInt(int value) => value <= 0 ? 0u : (uint)value;
}
