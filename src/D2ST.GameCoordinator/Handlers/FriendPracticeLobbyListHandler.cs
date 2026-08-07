using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Nobody's friends are hosting a lobby (7111 → 7112 with an empty list).
/// </summary>
public sealed class FriendPracticeLobbyListHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.GCFriendPracticeLobbyListRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgFriendPracticeLobbyListResponse();
        return
        [
            new GcMessage(GcMsg.GCFriendPracticeLobbyListResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
