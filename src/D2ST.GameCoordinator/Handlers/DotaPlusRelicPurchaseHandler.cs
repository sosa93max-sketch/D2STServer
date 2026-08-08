using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Purchases a locally persisted hero relic with local Dota Plus shards. The
/// response carries the deterministic local kill-eater type expected by the
/// client; the ownership record is kept on the LAN server.
/// </summary>
public sealed class DotaPlusRelicPurchaseHandler : IGcMessageHandler
{
    private readonly IDotaPlusStore _plus;

    public DotaPlusRelicPurchaseHandler(IDotaPlusStore plus)
    {
        _plus = plus;
    }

    public uint MessageType => GcMsg.PurchaseHeroRandomRelic;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var purchase = context.Codec.Decode<CMsgPurchaseHeroRandomRelic>(request.Body);
        var result = _plus.PurchaseRelic(
            context.AccountId,
            purchase.HeroId,
            (int)purchase.RelicRarity);
        var response = new CMsgPurchaseHeroRandomRelicResponse
        {
            Result = ResultFor(result.Code, result.Success),
            KillEaterType = result.KillEaterType
        };
        return
        [
            new GcMessage(
                GcMsg.PurchaseHeroRandomRelicResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }

    private static EPurchaseHeroRelicResult ResultFor(string code, bool success) => success
        ? EPurchaseHeroRelicResult.kEPurchaseHeroRelicResultSuccess
        : code switch
        {
            "not_enough_shards" => EPurchaseHeroRelicResult.kEPurchaseHeroRelicResultNotEnoughPoints,
            "invalid_rarity" => EPurchaseHeroRelicResult.kEPurchaseHeroRelicResultInvalidRarity,
            "invalid_relic" => EPurchaseHeroRelicResult.kEPurchaseHeroRelicResultInvalidRelic,
            "subscription_required" => EPurchaseHeroRelicResult.kEPurchaseHeroRelicResultPurchaseNotAllowed,
            _ => EPurchaseHeroRelicResult.kEPurchaseHeroRelicResultInternalServerError
        };
}
