using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Throws an account out of a private chat (8088 → 8091).
/// </summary>
public sealed class PrivateChatKickHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public PrivateChatKickHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.PrivateChatKick;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgClientToGCPrivateChatKick>(request.Body);
        var response = _chat.Kick(context, body);

        return
        [
            new GcMessage(GcMsg.ToClientPrivateChatResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
