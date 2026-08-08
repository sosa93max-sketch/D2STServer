namespace D2ST.Persistence;

/// <summary>Per-account/per-hero aggregate projection for the next profile views.</summary>
public sealed class PlayerHeroStatsEntity
{
    public uint AccountId { get; set; }

    public int HeroId { get; set; }

    public int Games { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public long TotalKills { get; set; }

    public long TotalDeaths { get; set; }

    public long TotalAssists { get; set; }

    public long TotalLastHits { get; set; }

    public long TotalDenies { get; set; }

    public long TotalHeroDamage { get; set; }

    public long TotalTowerDamage { get; set; }

    public long TotalHeroHealing { get; set; }

    public long TotalGoldSpent { get; set; }

    public long TotalGoldPerMin { get; set; }

    public long TotalXpPerMinute { get; set; }

    public DateTimeOffset? LastMatchAt { get; set; }
}
