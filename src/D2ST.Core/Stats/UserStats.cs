namespace D2ST.Core.Stats;

public sealed record StatValue(string Name, uint Data);

public sealed record AchievementValue(
    string Name,
    bool Earned,
    DateTimeOffset Date,
    uint Progress,
    uint MaxProgress);

/// <summary>Everything the client caches for one player's stats page.</summary>
public sealed record UserStats(
    ulong SteamId,
    IReadOnlyList<StatValue> Stats,
    IReadOnlyList<AchievementValue> Achievements,
    int CurrentPlayers);
