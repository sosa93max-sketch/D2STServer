using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>Stored profile-card slot layout for one account.</summary>
public sealed class ProfileCardEntity
{
    [Key]
    public uint AccountId { get; set; }

    public string SlotsJson { get; set; } = "[]";

    public DateTimeOffset UpdatedAt { get; set; }
}
