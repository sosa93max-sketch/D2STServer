using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No hero progress exists (7521 → 7522); the reply echoes the account.
/// </summary>
public sealed class GetAllHeroProgressHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCGetAllHeroProgress;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var progress = context.Codec.Decode<CMsgClientToGCGetAllHeroProgress>(request.Body);
        var response = new CMsgClientToGCGetAllHeroProgressResponse
        {
            AccountId = progress.AccountId != 0 ? progress.AccountId : context.AccountId
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCGetAllHeroProgressResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
