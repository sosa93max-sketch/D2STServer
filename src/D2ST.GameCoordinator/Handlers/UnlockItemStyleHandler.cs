using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Unlocking a locked style normally costs a key or a compendium level. There is
/// no catalogue to charge against, so an owned item's style is granted outright
/// and anything else fails as invalid.
/// </summary>
public sealed class UnlockItemStyleHandler : IGcMessageHandler
{
    private readonly EconInventory _inventory;

    public UnlockItemStyleHandler(EconInventory inventory)
    {
        _inventory = inventory;
    }

    public uint MessageType => GcMsg.UnlockItemStyle;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var unlock = context.Codec.Decode<CMsgClientToGCUnlockItemStyle>(request.Body);
        var owned = _inventory.TryGetItem(context.SteamId, unlock.ItemToUnlock, out _);

        var response = new CMsgClientToGCUnlockItemStyleResponse
        {
            ItemId = unlock.ItemToUnlock,
            StyleIndex = unlock.StyleIndex,
            Response = owned
                ? CMsgClientToGCUnlockItemStyleResponse.EUnlockStyle.kUnlockStyleSucceeded
                : CMsgClientToGCUnlockItemStyleResponse.EUnlockStyle.kUnlockStyleFailedItemIsInvalid
        };

        return
        [
            new GcMessage(GcMsg.UnlockItemStyleResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
