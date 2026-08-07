using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Chat;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// How busy a channel is (8048 → 8049), asked before joining it.
/// </summary>
public sealed class ChatGetMemberCountHandler : IGcMessageHandler
{
    private readonly ChatService _chat;

    public ChatGetMemberCountHandler(ChatService chat)
    {
        _chat = chat;
    }

    public uint MessageType => GcMsg.ChatGetMemberCount;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var body = context.Codec.Decode<CMsgDOTAChatGetMemberCount>(request.Body);
        var response = _chat.MemberCount(body);

        return
        [
            new GcMessage(GcMsg.ChatGetMemberCountResponse, context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
