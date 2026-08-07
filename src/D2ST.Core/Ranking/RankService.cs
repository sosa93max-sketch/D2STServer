namespace D2ST.Core.Ranking;

/// <summary>The medal a player's MMR maps to.</summary>
public sealed record RankInfo(int Tier, int Star)
{
    /// <summary>Dota's rank encoding: tier*10 + star (Herald 1 = 11 … Immortal 5 = 85).</summary>
    public int RankValue => Tier * 10 + Star;
}

/// <summary>A player's rating snapshot.</summary>
public sealed record PlayerRank(uint AccountId, int Mmr, int Wins, int Losses, int Games);

/// <summary>
/// Pure MMR math: Elo-style deltas for practice-lobby matches and the
/// Dota-style medal a rating maps to. Everyone starts at 0 MMR — Herald 1.
/// </summary>
public static class RankMath
{
    public const int KFactor = 32;
    private const int TierSpan = 770;

    private static readonly (int Max, int Tier)[] TierMax =
    {
        (769, 1), (1539, 2), (2299, 3), (3069, 4), (3839, 5), (4609, 6), (5379, 7), (int.MaxValue, 8)
    };

    /// <summary>The medal for an MMR: tier 1..8 (Herald..Immortal) and star 1..5.</summary>
    public static RankInfo RankFor(int mmr)
    {
        var tier = 1;
        foreach (var (max, candidate) in TierMax)
        {
            if (mmr <= max)
            {
                tier = candidate;
                break;
            }
        }

        var star = tier >= 8
            ? 1
            : Math.Clamp(((mmr - (tier - 1) * TierSpan) * 5) / TierSpan + 1, 1, 5);
        return new RankInfo(tier, star);
    }

    /// <summary>The MMR change for one player against the opponents' average rating.</summary>
    public static int Delta(int mmr, int opponentAverage, bool won)
    {
        var expected = 1.0 / (1.0 + Math.Pow(10, (opponentAverage - mmr) / 400.0));
        var delta = (int)Math.Round(KFactor * ((won ? 1.0 : 0.0) - expected));
        return delta == 0 ? (won ? 1 : -1) : delta;
    }
}
