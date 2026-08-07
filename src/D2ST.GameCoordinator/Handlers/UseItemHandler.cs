using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Consumes an item. Nothing is consumable yet, so an owned item reports as
/// used and an unknown one reports a server error, which the client shows as a
/// failed action instead of leaving the button spinning.
/// </summary>
public sealed class UseItemHandler : IGcMessageHandler
{
    private readonly EconInventory _inventory;

    public UseItemHandler(EconInventory inventory)
    {
        _inventory = inventory;
    }

    public uint MessageType => GcMsg.UseItemRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var use = context.Codec.Decode<CMsgUseItem>(request.Body);
        var owned = _inventory.TryGetItem(context.SteamId, use.ItemId, out _);

        var response = new CMsgGenericResult
        {
            Eresult = (uint)(owned
                ? EGCMsgUseItemResponse.kEGCMsgUseItemResponseItemUsed
                : EGCMsgUseItemResponse.kEGCMsgUseItemResponseServerError)
        };

        return
        [
            new GcMessage(GcMsg.UseItemResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
