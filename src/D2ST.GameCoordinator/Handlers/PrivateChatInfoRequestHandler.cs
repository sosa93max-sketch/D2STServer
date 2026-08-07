using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Who may enter a private chat and who is in it (8092 → 8093).
/// </summary>
public sealed class PrivateChatInfoRequestHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public PrivateChatInfoRequestHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.PrivateChatInfoRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgClientToGCPrivateChatInfoRequest>(request.Body);
        var response = _chat.PrivateChatInfo(context, body.PrivateChatChannelName);

        return
        [
            new GcMessage(GcMsg.ToClientPrivateChatInfoResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
