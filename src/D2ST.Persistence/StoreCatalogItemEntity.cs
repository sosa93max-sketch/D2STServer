using System.ComponentModel.DataAnnotations;
using D2ST.Core.Economy;

namespace D2ST.Persistence;

/// <summary>One locally sellable item or set.</summary>
public sealed class StoreCatalogItemEntity
{
    [Key]
    public uint ProductId { get; set; }

    /// <summary>Steam/Dota econ definition for an item; zero for a set.</summary>
    public uint DefIndex { get; set; }

    public StoreProductType ProductType { get; set; }

    public long PriceCredits { get; set; }

    [MaxLength(160)]
    public required string Name { get; set; }

    [MaxLength(64)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string Description { get; set; } = string.Empty;

    public uint BuildVersion { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
