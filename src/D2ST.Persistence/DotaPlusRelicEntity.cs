namespace D2ST.Persistence;

/// <summary>
/// Local ownership record for a hero relic purchase. The kill-eater type is
/// retained independently of the client item schema so the grant remains
/// deterministic across reconnects and client updates.
/// </summary>
public sealed class DotaPlusRelicEntity
{
    public long Id { get; set; }

    public uint AccountId { get; set; }

    public int HeroId { get; set; }

    public int RelicRarity { get; set; }

    public uint KillEaterType { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
