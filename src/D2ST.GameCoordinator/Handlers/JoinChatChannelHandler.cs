using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Joins a chat channel by name (7009 → 7010). The reply carries the whole
/// member list, which is what the client draws the channel from; the members
/// already there are told separately.
/// </summary>
public sealed class JoinChatChannelHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public JoinChatChannelHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.JoinChatChannel;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgDOTAJoinChatChannel>(request.Body);
        var response = _chat.Join(context, body);

        return
        [
            new GcMessage(GcMsg.JoinChatChannelResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
