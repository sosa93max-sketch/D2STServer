using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>
/// A leaderboard, created on demand by the first client that asks for it by
/// name (Steam's FindOrCreateLeaderboard).
/// </summary>
public sealed class LeaderboardEntity
{
    [Key]
    public int Id { get; set; }

    public uint AppId { get; set; }

    [MaxLength(128)]
    public required string Name { get; set; }

    public int SortMethod { get; set; }

    public int DisplayType { get; set; }
}

/// <summary>One player's best (or last forced) score on a leaderboard.</summary>
public sealed class LeaderboardEntryEntity
{
    public int LeaderboardId { get; set; }

    public uint AccountId { get; set; }

    public int Score { get; set; }

    /// <summary>Steam's opaque detail ints, stored comma separated.</summary>
    [MaxLength(512)]
    public string Details { get; set; } = string.Empty;

    public ulong UgcHandle { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
