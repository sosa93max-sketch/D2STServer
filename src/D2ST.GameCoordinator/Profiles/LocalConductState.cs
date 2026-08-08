using D2ST.Core.Matches;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Profiles;

/// <summary>
/// Conduct values for the private/local deployment. There is no report or
/// moderation service yet, so local accounts start with an explicit good
/// status instead of an omitted/zero score that the Dota client interprets as
/// restricted. Match counts still come from the persisted local history.
/// </summary>
internal static class LocalConductState
{
    public const uint BehaviorScore = 10_000;
    public const uint SequenceNumber = 1;

    public static void ApplyTo(CSODOTAGameAccountClient account)
    {
        account.PlayerBehaviorSeqNumLastReport = SequenceNumber;
        account.PlayerBehaviorScoreLastReport = BehaviorScore;
        account.PlayerBehaviorReportOldData = false;
        account.LowPriorityUntilDate = 0;
        account.LowPriorityGamesRemaining = 0;
        account.PreventTextChatUntilDate = 0;
        account.PreventVoiceUntilDate = 0;
        account.PreventPublicTextChatUntilDate = 0;
        account.PreventNewPlayerChatUntilDate = 0;
        account.AccountDisabledUntilDate = 0;
        account.AccountDisabledCount = 0;
        account.MatchDisabledUntilDate = 0;
        account.MatchDisabledCount = 0;
        account.RankedMatchmakingBanUntilDate = 0;
    }

    public static CMsgPlayerConductScorecard BuildScorecard(
        uint accountId,
        PlayerProfileStats stats)
    {
        var games = NonNegative(stats.Games);
        var abandoned = NonNegative(stats.LeaverCount);
        var clean = games >= abandoned ? games - abandoned : 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new CMsgPlayerConductScorecard
        {
            AccountId = accountId,
            MatchId = 0,
            SeqNum = SequenceNumber,
            Reasons = 0,
            MatchesInReport = games,
            MatchesClean = clean,
            MatchesReported = 0,
            MatchesAbandoned = abandoned,
            ReportsCount = 0,
            ReportsParties = 0,
            CommendCount = 0,
            Date = (uint)Math.Max(0, now),
            RawBehaviorScore = BehaviorScore,
            OldRawBehaviorScore = BehaviorScore,
            CommsReports = 0,
            CommsParties = 0,
            BehaviorRating = CMsgPlayerConductScorecard.EBehaviorRating.keBehaviorGood
        };
    }

    private static uint NonNegative(int value) => (uint)Math.Max(0, value);
}
