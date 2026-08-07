using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

public sealed class UserAchievementEntity
{
    public uint AccountId { get; set; }

    [MaxLength(128)]
    public required string Name { get; set; }

    public bool Earned { get; set; }

    public DateTimeOffset Date { get; set; }

    public uint Progress { get; set; }

    public uint MaxProgress { get; set; }
}
