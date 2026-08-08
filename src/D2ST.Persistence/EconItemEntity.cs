namespace D2ST.Persistence;

/// <summary>
/// Durable projection of the econ Shared Object. The GC cache is rebuilt from
/// these rows after reconnect or server restart.
/// </summary>
public sealed class EconItemEntity
{
    public ulong ItemId { get; set; }

    public uint AccountId { get; set; }

    public uint DefIndex { get; set; }

    public uint Quantity { get; set; }

    public uint Level { get; set; }

    public uint Quality { get; set; }

    public uint Flags { get; set; }

    public uint Origin { get; set; }

    public uint Inventory { get; set; }

    public uint Style { get; set; }

    public ulong OriginalId { get; set; }

    public string EquippedStatesJson { get; set; } = "[]";

    public string AttributesJson { get; set; } = "[]";

    public DateTimeOffset UpdatedAt { get; set; }
}
