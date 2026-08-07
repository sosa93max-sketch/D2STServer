using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Lets an account into a private chat (8084 → 8091).
/// </summary>
public sealed class PrivateChatInviteHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public PrivateChatInviteHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.PrivateChatInvite;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgClientToGCPrivateChatInvite>(request.Body);
        var response = _chat.Invite(context, body);

        return
        [
            new GcMessage(GcMsg.ToClientPrivateChatResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
