using D2ST.Core.GameCoordinator;
using D2ST.Core.Profiles;
using D2ST.GameCoordinator.Profiles;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Persists the profile-card layout (7538) and sends the updated card (7539).
/// The request does not carry an account id; the authenticated GC context is
/// therefore the only account allowed to edit.
/// </summary>
public sealed class SetProfileCardSlotsHandler : IGcMessageHandler
{
    private const int MaxSlots = 64;

    private readonly IProfileStore _profiles;
    private readonly ProfileCardBuilder _cards;

    public SetProfileCardSlotsHandler(IProfileStore profiles, ProfileCardBuilder cards)
    {
        _profiles = profiles;
        _cards = cards;
    }

    public uint MessageType => GcMsg.ClientToGCSetProfileCardSlots;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var changed = context.Codec.Decode<CMsgClientToGCSetProfileCardSlots>(request.Body)
            .Slots
            .Where(slot => slot.SlotId < MaxSlots && IsKnownSlotType(slot.SlotType))
            .GroupBy(slot => slot.SlotId)
            .Select(group => group.Last())
            .OrderBy(slot => slot.SlotId)
            .Select(slot => new ProfileCardSlot(
                slot.SlotId,
                (uint)slot.SlotType,
                slot.SlotValue))
            .ToArray();

        _profiles.SetCard(context.AccountId, changed);
        var updated = _cards.Build(context.AccountId);

        return
        [
            new GcMessage(
                GcMsg.GCToClientProfileCardUpdated,
                context.Codec.Encode(updated),
                TargetJobId: request.SourceJobId)
        ];
    }

    private static bool IsKnownSlotType(EProfileCardSlotType slotType) => slotType is
        EProfileCardSlotType.kEProfileCardSlotTypeEmpty or
        EProfileCardSlotType.kEProfileCardSlotTypeStat or
        EProfileCardSlotType.kEProfileCardSlotTypeTrophy or
        EProfileCardSlotType.kEProfileCardSlotTypeItem or
        EProfileCardSlotType.kEProfileCardSlotTypeHero or
        EProfileCardSlotType.kEProfileCardSlotTypeEmoticon or
        EProfileCardSlotType.kEProfileCardSlotTypeTeam;
}
