using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Unpacks a bundle into its parts. Bundles have no contents without a
/// catalogue, so the request fails as an invalid item.
/// </summary>
public sealed class UnpackBundleHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.UnpackBundle;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        _ = context.Codec.Decode<CMsgClientToGCUnpackBundle>(request.Body);
        var response = new CMsgClientToGCUnpackBundleResponse
        {
            Response = CMsgClientToGCUnpackBundleResponse.EUnpackBundle.kUnpackBundleFailedItemIsInvalid
        };

        return
        [
            new GcMessage(GcMsg.UnpackBundleResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
