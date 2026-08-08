using D2ST.Core.Profiles;

namespace D2ST.GameCoordinator.Profiles;

/// <summary>Fallback for hosts that run the GC without a persistence adapter.</summary>
public sealed class EmptyShowcaseStore : IShowcaseStore
{
    public ShowcaseRecord? Get(uint accountId, uint showcaseType) => null;

    public void Set(uint accountId, uint showcaseType, uint formatVersion, byte[] payload)
    {
    }
}
