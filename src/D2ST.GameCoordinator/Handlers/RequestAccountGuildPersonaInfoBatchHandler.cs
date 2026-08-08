using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// No guilds are provisioned locally. The stock client still expects one
/// empty persona container per requested account when it opens profile and
/// social panels.
/// </summary>
public sealed class RequestAccountGuildPersonaInfoBatchHandler : IGcMessageHandler
{
    public uint MessageType => GcMsg.ClientToGCRequestAccountGuildPersonaInfoBatch;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var requested = context.Codec.Decode<CMsgClientToGCRequestAccountGuildPersonaInfoBatch>(request.Body);
        var response = new CMsgClientToGCRequestAccountGuildPersonaInfoBatchResponse
        {
            Result = CMsgClientToGCRequestAccountGuildPersonaInfoBatchResponse.EResponse.keSuccess
        };

        foreach (var _ in requested.AccountIds ?? Array.Empty<uint>())
        {
            response.PersonaInfos.Add(new CMsgAccountGuildsPersonaInfo());
        }

        return
        [
            new GcMessage(
                GcMsg.ClientToGCRequestAccountGuildPersonaInfoBatchResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
