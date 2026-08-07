using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Takes the admin flag off a member of a private chat (8090 → 8091).
/// </summary>
public sealed class PrivateChatDemoteHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public PrivateChatDemoteHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.PrivateChatDemote;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgClientToGCPrivateChatDemote>(request.Body);
        var response = _chat.Demote(context, body);

        return
        [
            new GcMessage(GcMsg.ToClientPrivateChatResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
