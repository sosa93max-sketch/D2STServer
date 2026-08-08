using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Matches;
using D2ST.GameCoordinator.Profiles;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Supplies the conduct and feature-gate values requested by a game server
/// before a match. A missing 7451 response leaves the server with zero/unknown
/// values, which the Dota client treats as restricted communication and
/// behavior state.
/// </summary>
public sealed class BatchPlayerResourcesHandler : IGcMessageHandler
{
    private readonly IMatchStore _matches;

    public BatchPlayerResourcesHandler(IMatchStore matches)
    {
        _matches = matches;
    }

    public uint MessageType => GcMsg.ServerToGCRequestBatchPlayerResources;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var resourceRequest = context.Codec.Decode<CMsgServerToGCRequestBatchPlayerResources>(request.Body);
        var response = new CMsgServerToGCRequestBatchPlayerResourcesResponse();

        foreach (var accountId in resourceRequest.AccountIds ?? Array.Empty<uint>())
        {
            response.Results.Add(
                LocalConductState.BuildPlayerResources(
                    accountId,
                    _matches.GetProfileStats(accountId)));
        }

        return
        [
            new GcMessage(
                GcMsg.ServerToGCRequestBatchPlayerResourcesResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
