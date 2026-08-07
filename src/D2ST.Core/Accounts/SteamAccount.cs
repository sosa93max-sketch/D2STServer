namespace D2ST.Core.Accounts;

/// <summary>
/// A resolved player identity. <see cref="SteamId"/> is the 64-bit Steam id and
/// <see cref="AccountId"/> is its low 32 bits, which is what the Dota GC uses in
/// most of its messages.
/// </summary>
public sealed record SteamAccount(ulong SteamId, string Username)
{
    public const ulong SteamIdBase = 76561197960265728UL;

    public uint AccountId => (uint)(SteamId - SteamIdBase);

    public static ulong SteamIdFromAccountId(uint accountId) => SteamIdBase + accountId;

    /// <summary>
    /// Low 32 bits of a 64-bit Steam id. Game server identities live in another
    /// account type, so the arithmetic form (<see cref="SteamIdFromAccountId"/>
    /// in reverse) does not hold for them, but the account id always does.
    /// </summary>
    public static uint AccountIdFromSteamId(ulong steamId) => (uint)(steamId & 0xFFFFFFFFUL);
}
