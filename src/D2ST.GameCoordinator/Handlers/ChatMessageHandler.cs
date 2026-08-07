using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// One chat line (7273). It is not answered but broadcast: every member of the
/// channel, the sender included, receives the same 7273 back with the author
/// the server stamped on it.
/// </summary>
public sealed class ChatMessageHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public ChatMessageHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.ChatMessage;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgDOTAChatMessage>(request.Body);
        _chat.Send(context, body);

        return [];
    }
}
