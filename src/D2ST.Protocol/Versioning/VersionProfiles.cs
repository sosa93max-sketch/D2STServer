namespace D2ST.Protocol.Versioning;

/// <summary>
/// Registry of known <see cref="VersionProfile"/>s and the ClientVersion-based
/// selection between them. Adding a supported build = add a profile and extend
/// <see cref="Resolve"/>; the modern path stays the default.
/// </summary>
public static class VersionProfiles
{
    public static readonly VersionProfile Modern = new(Name: "modern", SocacheFileVersion: 20, IncludeDotaPlus: true);

    /// <summary>
    /// The current target build (steam.inf ClientVersion 6783, May 2026). The
    /// SO cache file version is the same as the legacy profile until a real
    /// client capture proves otherwise.
    /// </summary>
    public static readonly VersionProfile V6783 = new(Name: "v6783", SocacheFileVersion: 20, IncludeDotaPlus: true);

    /// <summary>Dota 2 7.22g (steam.inf ClientVersion 3756, Sep 2019).</summary>
    public static readonly VersionProfile V722g = new(Name: "v722g", SocacheFileVersion: 20, IncludeDotaPlus: true);

    /// <summary>
    /// Inclusive upper bound of the ClientVersion range served by the 7.22g-era
    /// profile: builds at or below this use <see cref="V722g"/>, newer builds use
    /// <see cref="Modern"/>. Modern Dota reports much higher ClientVersions.
    /// </summary>
    public const int Legacy722gMaxClientVersion = 3756;
    public const int Target6783MinClientVersion = 6783;

    public static VersionProfile Resolve(int clientVersion)
    {
        if (clientVersion >= Target6783MinClientVersion)
        {
            return V6783;
        }

        if (clientVersion > 0 && clientVersion <= Legacy722gMaxClientVersion)
        {
            return V722g;
        }

        return Modern;
    }
}
