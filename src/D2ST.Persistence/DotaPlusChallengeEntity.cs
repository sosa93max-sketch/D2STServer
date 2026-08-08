using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>Durable local projection of one player's Dota Plus challenge.</summary>
public sealed class DotaPlusChallengeEntity
{
    public uint AccountId { get; set; }

    public uint SlotId { get; set; }

    public uint EventId { get; set; }

    public uint IntParam0 { get; set; }

    public uint IntParam1 { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public uint Completed { get; set; }

    public uint SequenceId { get; set; }

    public uint ChallengeTier { get; set; }

    public uint Flags { get; set; }

    public uint Attempts { get; set; }

    public uint CompleteLimit { get; set; }

    public uint QuestRank { get; set; }

    public uint MaxQuestRank { get; set; }

    public uint InstanceId { get; set; }

    public int HeroId { get; set; }

    public uint TemplateId { get; set; }

    [MaxLength(64)]
    public required string LastMatchReference { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
