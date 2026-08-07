using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Equips cosmetics on a hero slot. The reply carries the econ cache version the
/// equip produced: the client waits for it to line up with the SO deltas the
/// inventory published before it stops showing the loadout as pending.
/// </summary>
public sealed class EquipItemsHandler : IGcMessageHandler
{
    private readonly EconInventory _inventory;

    public EquipItemsHandler(EconInventory inventory)
    {
        _inventory = inventory;
    }

    public uint MessageType => GcMsg.EquipItems;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var equip = context.Codec.Decode<CMsgClientToGCEquipItems>(request.Body);
        _inventory.Equip(context.SteamId, equip.Equips);

        var response = new CMsgClientToGCEquipItemsResponse
        {
            SoCacheVersionId = _inventory.CacheVersion(context.SteamId)
        };

        return
        [
            new GcMessage(GcMsg.EquipItemsResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
