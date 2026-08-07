using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No Monster Hunter progress exists (9023 → 9024, success, empty).
/// </summary>
public sealed class MonsterHunterGetUserDataHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCMonsterHunterGetUserData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgClientToGCMonsterHunterGetUserDataResponse
        {
            Response = CMsgClientToGCMonsterHunterGetUserDataResponse.EResponse.keSuccess
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCMonsterHunterGetUserDataResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
