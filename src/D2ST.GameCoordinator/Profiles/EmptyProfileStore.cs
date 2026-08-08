using D2ST.Core.Profiles;

namespace D2ST.GameCoordinator.Profiles;

/// <summary>
/// Keeps the reusable GC host usable without a persistence adapter. The API
/// host replaces this registration with the SQLite-backed implementation.
/// </summary>
internal sealed class EmptyProfileStore : IProfileStore
{
    public ProfileCardData GetCard(uint accountId) => ProfileCardData.Empty;

    public void SetCard(uint accountId, IReadOnlyList<ProfileCardSlot> slots)
    {
    }
}
