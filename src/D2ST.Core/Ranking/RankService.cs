namespace D2ST.Core.Ranking;

/// <summary>The medal and progress a player's MMR maps to.</summary>
public sealed record RankInfo(int Tier, int Star, int ProgressPercent)
{
    /// <summary>
    /// Dota's rank encoding: tier*10 + star (Herald 1 = 11 … Divine 5 = 75,
    /// Immortal = 80). Zero is the uncalibrated value.
    /// </summary>
    public int RankValue => Tier == 0 ? 0 : Tier * 10 + Star;

    public static RankInfo Uncalibrated => new(0, 0, 0);
}

/// <summary>A player's rating snapshot.</summary>
public sealed record PlayerRank(
    uint AccountId,
    int Mmr,
    int Wins,
    int Losses,
    int Games,
    bool IsCalibrated);

/// <summary>
/// Pure MMR math: Elo-style deltas for practice-lobby matches and the
/// Dota-style medal a rating maps to. A zero-MMR account is only visible as a
/// medal after it has been marked calibrated; this keeps the protocol's
/// uncalibrated value separate from the lowest visible medal.
/// </summary>
public static class RankMath
{
    public const int KFactor = 32;

    // Commonly documented current Dota medal bands. The client does not want MMR in
    // rank_tier_score; it wants the percentage through the current star.
    // Keeping the bands explicit also avoids the old 770-point approximation,
    // which put the promotion boundaries in the wrong places.
    private static readonly MedalBand[] MedalBands =
    [
        new(0, 153, 11),
        new(154, 307, 12),
        new(308, 461, 13),
        new(462, 615, 14),
        new(616, 769, 15),
        new(770, 923, 21),
        new(924, 1077, 22),
        new(1078, 1231, 23),
        new(1232, 1385, 24),
        new(1386, 1539, 25),
        new(1540, 1693, 31),
        new(1694, 1847, 32),
        new(1848, 2001, 33),
        new(2002, 2155, 34),
        new(2156, 2309, 35),
        new(2310, 2463, 41),
        new(2464, 2617, 42),
        new(2618, 2771, 43),
        new(2772, 2925, 44),
        new(2926, 3079, 45),
        new(3080, 3233, 51),
        new(3234, 3387, 52),
        new(3388, 3541, 53),
        new(3542, 3695, 54),
        new(3696, 3849, 55),
        new(3850, 4003, 61),
        new(4004, 4157, 62),
        new(4158, 4311, 63),
        new(4312, 4465, 64),
        new(4466, 4619, 65),
        new(4620, 4819, 71),
        new(4820, 5019, 72),
        new(5020, 5219, 73),
        new(5220, 5419, 74),
        new(5420, 5619, 75)
    ];

    /// <summary>
    /// The medal for an MMR: tier 1..8 (Herald..Immortal), star 1..5, and
    /// progress from 0 to 100 within that star.
    /// </summary>
    public static RankInfo RankFor(int mmr)
    {
        mmr = Math.Max(0, mmr);
        foreach (var band in MedalBands)
        {
            if (mmr >= band.Min && mmr <= band.Max)
            {
                var progress = (int)((long)(mmr - band.Min) * 100 / (band.Max - band.Min));
                return new(band.RankValue / 10, band.RankValue % 10, progress);
            }
        }

        // Immortal has no star band. Keeping its progress at 100 matches the
        // established GC implementations and prevents a missing-value path.
        return new RankInfo(8, 0, 100);
    }

    /// <summary>Returns the value that should be exposed to the Dota client.</summary>
    public static RankInfo VisibleRankFor(PlayerRank rank) =>
        rank.IsCalibrated ? RankFor(rank.Mmr) : RankInfo.Uncalibrated;

    private sealed record MedalBand(int Min, int Max, int RankValue);

    /// <summary>The MMR change for one player against the opponents' average rating.</summary>
    public static int Delta(int mmr, int opponentAverage, bool won)
    {
        var expected = 1.0 / (1.0 + Math.Pow(10, (opponentAverage - mmr) / 400.0));
        var delta = (int)Math.Round(KFactor * ((won ? 1.0 : 0.0) - expected));
        return delta == 0 ? (won ? 1 : -1) : delta;
    }
}
