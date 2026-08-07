using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// The channel list the chat window offers (7060 → 7061): the channels the
/// server configured plus whatever players opened, never a private chat.
/// </summary>
public sealed class RequestChatChannelListHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public RequestChatChannelListHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.RequestChatChannelList;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgDOTARequestChatChannelList>(request.Body);
        var response = _chat.List();

        return
        [
            new GcMessage(GcMsg.RequestChatChannelListResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
