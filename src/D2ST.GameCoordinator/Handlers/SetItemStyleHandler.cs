using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Econ;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Selects the variant of an item the player already owns. Failure is reported
/// rather than silently ignored so the client reverts its preview instead of
/// showing a style the cache never received.
/// </summary>
public sealed class SetItemStyleHandler : IGcMessageHandler
{
    private readonly EconInventory _inventory;

    public SetItemStyleHandler(EconInventory inventory)
    {
        _inventory = inventory;
    }

    public uint MessageType => GcMsg.SetItemStyle;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var set = context.Codec.Decode<CMsgClientToGCSetItemStyle>(request.Body);
        var applied = _inventory.SetStyle(context.SteamId, set.ItemId, set.StyleIndex);

        var response = new CMsgClientToGCSetItemStyleResponse
        {
            Response = applied
                ? CMsgClientToGCSetItemStyleResponse.ESetStyle.kSetStyleSucceeded
                : CMsgClientToGCSetItemStyleResponse.ESetStyle.kSetStyleFailed
        };

        return
        [
            new GcMessage(GcMsg.SetItemStyleResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
