using System.ComponentModel.DataAnnotations;
using D2ST.Core.Economy;

namespace D2ST.Persistence;

/// <summary>One locally sellable item, set or Dota Plus subscription plan.</summary>
public sealed class StoreCatalogItemEntity
{
    [Key]
    public uint ProductId { get; set; }

    /// <summary>Steam/Dota econ definition for an item; zero for a set.</summary>
    public uint DefIndex { get; set; }

    public StoreProductType ProductType { get; set; }

    public long PriceDollars { get; set; }

    /// <summary>
    /// Exact Steam Community Market name. It is nullable in practice because
    /// many client definitions are not marketable.
    /// </summary>
    [MaxLength(300)]
    public string MarketHashName { get; set; } = string.Empty;

    /// <summary>
    /// English client name used to resolve the exact Steam market hash when
    /// the catalog display name is localized for the player.
    /// </summary>
    [MaxLength(300)]
    public string MarketSearchName { get; set; } = string.Empty;

    /// <summary>Lowest USD market price in cents from the last sync.</summary>
    public long? MarketLowestPriceCents { get; set; }

    /// <summary>Median USD market price in cents from the last sync.</summary>
    public long? MarketMedianPriceCents { get; set; }

    /// <summary>Reported market volume from the last sync.</summary>
    public long? MarketVolume { get; set; }

    [MaxLength(32)]
    public string MarketPriceSource { get; set; } = string.Empty;

    [MaxLength(32)]
    public string MarketPriceStatus { get; set; } = "not_checked";

    public DateTimeOffset? MarketPriceUpdatedAt { get; set; }

    [MaxLength(160)]
    public required string Name { get; set; }

    [MaxLength(64)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string Description { get; set; } = string.Empty;

    /// <summary>JSON array of hero names that can use the cosmetic.</summary>
    [MaxLength(4096)]
    public string HeroesJson { get; set; } = "[]";

    public uint BuildVersion { get; set; }

    /// <summary>Subscription duration for a Dota Plus product; zero for items and sets.</summary>
    public int DotaPlusDays { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
