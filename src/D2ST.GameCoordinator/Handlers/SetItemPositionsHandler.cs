using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Records where the player dragged items in the armory grid. The client sends
/// this fire-and-forget and re-reads the positions from the econ cache, so the
/// only reply is the SO update the inventory publishes.
/// </summary>
public sealed class SetItemPositionsHandler : IGcMessageHandler
{
    private readonly EconInventory _inventory;

    public SetItemPositionsHandler(EconInventory inventory)
    {
        _inventory = inventory;
    }

    public uint MessageType => GcMsg.SetItemPositions;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var positions = context.Codec.Decode<CMsgSetItemPositions>(request.Body);
        _inventory.SetPositions(context.SteamId, positions.ItemPositions);
        return [];
    }
}
