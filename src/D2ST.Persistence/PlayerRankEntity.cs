using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>
/// One player's rating. Matches played in a practice lobby are rated with a
/// small Elo-like delta: winners gain, losers lose, starting from 0 MMR. The
/// calibration bit is kept separately because MMR alone does not tell the
/// client whether a medal is visible yet.
/// </summary>
public sealed class PlayerRankEntity
{
    [Key]
    public uint AccountId { get; set; }

    public int Mmr { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Games { get; set; }

    public bool IsCalibrated { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
