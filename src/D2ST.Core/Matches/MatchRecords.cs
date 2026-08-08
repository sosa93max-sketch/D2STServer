namespace D2ST.Core.Matches;

/// <summary>
/// The normalized result reported by the game server when a local lobby match
/// ends. The protocol-facing handler maps the generated protobuf into this
/// model before the host persists it.
/// </summary>
public sealed record MatchRecord
{
    public required ulong MatchId { get; init; }

    public ulong LobbyId { get; init; }

    public uint GameMode { get; init; }

    public uint DurationSeconds { get; init; }

    public required DateTimeOffset EndedAt { get; init; }

    public bool GoodGuysWin { get; init; }

    public int WinningTeam { get; init; }

    public uint FirstBloodTime { get; init; }

    public uint RadiantScore { get; init; }

    public uint DireScore { get; init; }

    public IReadOnlyList<uint> TowerStatus { get; init; } = Array.Empty<uint>();

    public IReadOnlyList<uint> BarracksStatus { get; init; } = Array.Empty<uint>();

    public IReadOnlyList<uint> TeamScores { get; init; } = Array.Empty<uint>();

    public uint Cluster { get; init; }

    public string ServerAddress { get; init; } = string.Empty;

    public uint EventScore { get; init; }

    public bool AutomaticSurrender { get; init; }

    public uint ServerVersion { get; init; }

    public uint PreGameDuration { get; init; }

    public int AverageNetworthDelta { get; init; }

    public uint MatchFlags { get; init; }

    public IReadOnlyList<MatchPlayerRecord> Players { get; init; } = Array.Empty<MatchPlayerRecord>();
}

/// <summary>One participant's final scoreboard and inventory snapshot.</summary>
public sealed record MatchPlayerRecord
{
    public required ulong SteamId { get; init; }

    public required uint AccountId { get; init; }

    public required int Team { get; init; }

    public int HeroId { get; init; }

    public bool Won { get; init; }

    public uint Gold { get; init; }

    public uint Kills { get; init; }

    public uint Deaths { get; init; }

    public uint Assists { get; init; }

    public uint LeaverStatus { get; init; }

    public uint LastHits { get; init; }

    public uint Denies { get; init; }

    public uint GoldPerMin { get; init; }

    public uint XpPerMinute { get; init; }

    public uint GoldSpent { get; init; }

    public uint Level { get; init; }

    public uint ScaledHeroDamage { get; init; }

    public uint ScaledTowerDamage { get; init; }

    public uint ScaledHeroHealing { get; init; }

    public uint TimeLastSeen { get; init; }

    public uint SupportAbilityValue { get; init; }

    public ulong PartyId { get; init; }

    public uint ClaimedFarmGold { get; init; }

    public uint SupportGold { get; init; }

    public uint ClaimedDenies { get; init; }

    public uint ClaimedMisses { get; init; }

    public uint Misses { get; init; }

    public uint NetWorth { get; init; }

    public uint HeroDamage { get; init; }

    public uint TowerDamage { get; init; }

    public uint HeroHealing { get; init; }

    public uint MatchPlayerFlags { get; init; }

    public uint HeroPickOrder { get; init; }

    public bool HeroWasRandomed { get; init; }

    public uint Lane { get; init; }

    public IReadOnlyList<int> Items { get; init; } = Array.Empty<int>();

    public IReadOnlyList<uint> ItemPurchaseTimes { get; init; } = Array.Empty<uint>();
}

/// <summary>
/// Counters shown by the account Shared Object. Per-match rows remain the
/// source of truth; this aggregate is only a fast profile projection.
/// </summary>
public sealed record PlayerProfileStats
{
    public required uint AccountId { get; init; }

    public int Games { get; init; }

    public int Wins { get; init; }

    public int Losses { get; init; }

    public long TotalKills { get; init; }

    public long TotalDeaths { get; init; }

    public long TotalAssists { get; init; }

    public long TotalLastHits { get; init; }

    public long TotalDenies { get; init; }

    public long TotalHeroDamage { get; init; }

    public long TotalTowerDamage { get; init; }

    public long TotalHeroHealing { get; init; }

    public long TotalGoldSpent { get; init; }

    public long TotalGoldPerMin { get; init; }

    public long TotalXpPerMinute { get; init; }

    public long TotalPlayTimeSeconds { get; init; }

    public int LeaverCount { get; init; }

    public DateTimeOffset? LastMatchAt { get; init; }

    public static PlayerProfileStats Empty(uint accountId) => new() { AccountId = accountId };
}
