namespace D2ST.Persistence;

/// <summary>One component product contained by a catalog set.</summary>
public sealed class StoreCatalogComponentEntity
{
    public uint ProductId { get; set; }

    public uint ComponentProductId { get; set; }

    public uint Quantity { get; set; }
}
