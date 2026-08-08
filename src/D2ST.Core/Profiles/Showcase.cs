namespace D2ST.Core.Profiles;

/// <summary>
/// One persisted profile showcase payload. The payload is the protobuf-encoded
/// <c>CMsgShowcase</c>; keeping it opaque here lets the GC preserve every
/// showcase item supported by the targeted client build without coupling the
/// persistence layer to generated protocol classes.
/// </summary>
public sealed record ShowcaseRecord(
    uint ShowcaseType,
    uint FormatVersion,
    byte[] Payload);

/// <summary>
/// Durable storage boundary for profile and mini-profile showcases.
/// </summary>
public interface IShowcaseStore
{
    ShowcaseRecord? Get(uint accountId, uint showcaseType);

    void Set(uint accountId, uint showcaseType, uint formatVersion, byte[] payload);
}

/// <summary>
/// Canonical showcase types supported by the build-6783 client. The default
/// variants are aliases used by read requests and share the same saved data.
/// </summary>
public static class ShowcaseTypes
{
    public const uint Profile = 1;
    public const uint MiniProfile = 2;
    public const uint DefaultProfile = 3;
    public const uint DefaultMiniProfile = 4;

    public static uint Canonical(uint showcaseType) => showcaseType switch
    {
        Profile or DefaultProfile => Profile,
        MiniProfile or DefaultMiniProfile => MiniProfile,
        _ => 0
    };
}
