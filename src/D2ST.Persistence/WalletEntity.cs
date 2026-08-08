namespace D2ST.Persistence;

/// <summary>Current balance and reserved amount for one local-economy account.</summary>
public sealed class WalletEntity
{
    public uint AccountId { get; set; }

    public long BalanceCredits { get; set; }

    public long ReservedCredits { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
