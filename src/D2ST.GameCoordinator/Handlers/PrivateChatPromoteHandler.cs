using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Makes a member of a private chat an admin of it (8089 → 8091).
/// </summary>
public sealed class PrivateChatPromoteHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public PrivateChatPromoteHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.PrivateChatPromote;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgClientToGCPrivateChatPromote>(request.Body);
        var response = _chat.Promote(context, body);

        return
        [
            new GcMessage(GcMsg.ToClientPrivateChatResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
