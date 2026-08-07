using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>
/// One player's rating. Matches played in a practice lobby are rated with a
/// small Elo-like delta: winners gain, losers lose, starting from 0 (Herald 1,
/// the lowest medal).
/// </summary>
public sealed class PlayerRankEntity
{
    [Key]
    public uint AccountId { get; set; }

    public int Mmr { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Games { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
