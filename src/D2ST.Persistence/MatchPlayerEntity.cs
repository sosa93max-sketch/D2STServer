namespace D2ST.Persistence;

/// <summary>Final scoreboard row for one account in a completed match.</summary>
public sealed class MatchPlayerEntity
{
    public ulong MatchId { get; set; }

    public uint AccountId { get; set; }

    public ulong SteamId { get; set; }

    public int Team { get; set; }

    public int HeroId { get; set; }

    public bool Won { get; set; }

    public uint Gold { get; set; }

    public uint Kills { get; set; }

    public uint Deaths { get; set; }

    public uint Assists { get; set; }

    public uint LeaverStatus { get; set; }

    public uint LastHits { get; set; }

    public uint Denies { get; set; }

    public uint GoldPerMin { get; set; }

    public uint XpPerMinute { get; set; }

    public uint GoldSpent { get; set; }

    public uint Level { get; set; }

    public uint ScaledHeroDamage { get; set; }

    public uint ScaledTowerDamage { get; set; }

    public uint ScaledHeroHealing { get; set; }

    public uint TimeLastSeen { get; set; }

    public uint SupportAbilityValue { get; set; }

    public ulong PartyId { get; set; }

    public uint ClaimedFarmGold { get; set; }

    public uint SupportGold { get; set; }

    public uint ClaimedDenies { get; set; }

    public uint ClaimedMisses { get; set; }

    public uint Misses { get; set; }

    public uint NetWorth { get; set; }

    public uint HeroDamage { get; set; }

    public uint TowerDamage { get; set; }

    public uint HeroHealing { get; set; }

    public uint MatchPlayerFlags { get; set; }

    public uint HeroPickOrder { get; set; }

    public bool HeroWasRandomed { get; set; }

    public uint Lane { get; set; }

    public string ItemsJson { get; set; } = "[]";

    public string ItemPurchaseTimesJson { get; set; } = "[]";

    public MatchEntity Match { get; set; } = null!;
}
