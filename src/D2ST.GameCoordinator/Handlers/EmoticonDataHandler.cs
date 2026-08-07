using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Publishes the caller's emoticon unlocks. <c>unlocked_emoticons</c> is a
/// bitfield indexed by emoticon id; an empty one means nothing is unlocked,
/// which is the correct answer until the econ side owns emoticons.
/// </summary>
public sealed class EmoticonDataHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.EmoticonDataRequest;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var response = new CMsgGCToClientEmoticonData
        {
            EmoticonAccess = new CMsgDOTAEmoticonAccessSDO
            {
                AccountId = context.AccountId,
                UnlockedEmoticons = []
            }
        };

        return
        [
            new GcMessage(
                GcMsg.ToClientEmoticonData,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
