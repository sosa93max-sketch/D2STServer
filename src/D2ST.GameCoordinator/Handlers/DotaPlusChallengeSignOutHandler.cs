using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Accepts the challenge progress packet emitted by a local game server at
/// match end. The regular 7004 path also updates challenges from the final
/// scoreboard; this handler makes the dedicated 7587 packet idempotent too.
/// </summary>
public sealed class DotaPlusChallengeSignOutHandler : IGcMessageHandler
{
    private readonly IDotaPlusStore _plus;
    private readonly DotaPlusProjection _projection;

    public DotaPlusChallengeSignOutHandler(
        IDotaPlusStore plus,
        DotaPlusProjection projection)
    {
        _plus = plus;
        _projection = projection;
    }

    public uint MessageType => GcMsg.SignOutUpdatePlayerChallenge;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var signOut = context.Codec.Decode<CMsgSignOutUpdatePlayerChallenge>(request.Body);
        var accountId = signOut.AccountId != 0 ? signOut.AccountId : context.AccountId;
        var reports = signOut.Completeds
            .Concat(signOut.Rerolleds)
            .Select(challenge => new DotaPlusChallengeReport(
                challenge.SlotId,
                challenge.SequenceId,
                challenge.Progress,
                challenge.ChallengeRank))
            .ToArray();
        _plus.ApplyChallengeReport(accountId, signOut.MatchId, signOut.HeroId, reports);
        _projection.RefreshChallenges(accountId);
        return [];
    }
}
