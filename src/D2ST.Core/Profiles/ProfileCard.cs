namespace D2ST.Core.Profiles;

/// <summary>One user-selected profile-card slot in its wire-compatible form.</summary>
public sealed record ProfileCardSlot(uint SlotId, uint SlotType, ulong SlotValue);

/// <summary>Persisted profile-card layout for one local account.</summary>
public sealed record ProfileCardData(IReadOnlyList<ProfileCardSlot> Slots)
{
    public static ProfileCardData Empty { get; } = new(Array.Empty<ProfileCardSlot>());
}

/// <summary>
/// Persistence boundary for profile-card layout. The GC does not depend on
/// EF Core; the API host supplies the durable implementation.
/// </summary>
public interface IProfileStore
{
    ProfileCardData GetCard(uint accountId);

    void SetCard(uint accountId, IReadOnlyList<ProfileCardSlot> slots);
}
