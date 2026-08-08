namespace D2ST.Core.Economy;

/// <summary>Rules for the local virtual economy.</summary>
public static class EconomyRules
{
    /// <summary>Dollars awarded to each eligible human player who wins a match.</summary>
    public const long MatchWinRewardDollars = 1;
}

/// <summary>Reason recorded in the immutable wallet ledger.</summary>
public enum EconomyTransactionKind
{
    MatchWinReward = 1,
    StorePurchase = 2,
    AdminAdjustment = 3,
    Refund = 4
}

/// <summary>Products sold by the local catalog.</summary>
public enum StoreProductType
{
    Item = 0,
    Set = 1,
    DotaPlusSubscription = 2
}
