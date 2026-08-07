using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Everyone in a channel (7403 → 7404), for the member list beside it.
/// </summary>
public sealed class ChatGetUserListHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public ChatGetUserListHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.ChatGetUserList;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgDOTAChatGetUserList>(request.Body);
        var response = _chat.UserList(body.ChannelId);

        return
        [
            new GcMessage(GcMsg.ChatGetUserListResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
