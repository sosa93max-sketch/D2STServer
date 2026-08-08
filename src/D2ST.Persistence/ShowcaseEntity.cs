using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>Stored profile or mini-profile showcase for one account.</summary>
public sealed class ShowcaseEntity
{
    public uint AccountId { get; set; }

    public uint ShowcaseType { get; set; }

    public uint FormatVersion { get; set; }

    [Required]
    public byte[] Payload { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; }
}
