using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Leaves a channel (7272). There is no reply: the client closed the tab
/// before it sent this, and only the other members have something to learn.
/// </summary>
public sealed class LeaveChatChannelHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public LeaveChatChannelHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.LeaveChatChannel;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgDOTALeaveChatChannel>(request.Body);
        _chat.Leave(context, body.ChannelId);

        return [];
    }
}
