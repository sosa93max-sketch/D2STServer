using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>Renews one local Dota Plus challenge and pushes the SO delta.</summary>
public sealed class DotaPlusRerollPlayerChallengeHandler : IGcMessageHandler
{
    private readonly IDotaPlusStore _plus;
    private readonly DotaPlusProjection _projection;

    public DotaPlusRerollPlayerChallengeHandler(
        IDotaPlusStore plus,
        DotaPlusProjection projection)
    {
        _plus = plus;
        _projection = projection;
    }

    public uint MessageType => GcMsg.ClientToGCRerollPlayerChallenge;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var reroll = context.Codec.Decode<CMsgClientToGCRerollPlayerChallenge>(request.Body);
        var result = _plus.RerollChallenge(context.AccountId, reroll.SequenceId, reroll.HeroId);
        if (result.Success)
        {
            _projection.RefreshChallenges(context.AccountId);
        }

        var response = new CMsgGCRerollPlayerChallengeResponse
        {
            Result = ResultFor(result.Code, result.Success)
        };
        return
        [
            new GcMessage(
                GcMsg.GCRerollPlayerChallengeResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }

    private static CMsgGCRerollPlayerChallengeResponse.EResult ResultFor(
        string code,
        bool success) => success
            ? CMsgGCRerollPlayerChallengeResponse.EResult.eResultSuccess
            : code == "challenge_not_found"
                ? CMsgGCRerollPlayerChallengeResponse.EResult.eResultNotFound
                : code == "subscription_required"
                    ? CMsgGCRerollPlayerChallengeResponse.EResult.eResultCantReroll
                    : CMsgGCRerollPlayerChallengeResponse.EResult.eResultServerError;
}
