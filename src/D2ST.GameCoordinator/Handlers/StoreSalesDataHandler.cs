using D2ST.Core.GameCoordinator;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Answers the store sale query. The client sends the version of the sales data
/// it already cached; echoing that version back means "nothing new", and the
/// expiration tells it when to ask again.
/// </summary>
public sealed class StoreSalesDataHandler : IGcMessageHandler
{
    private static readonly TimeSpan Validity = TimeSpan.FromDays(1);

    private readonly TimeProvider _timeProvider;

    public StoreSalesDataHandler(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public uint MessageType => GcMsg.RequestStoreSalesData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var requested = context.Codec.Decode<CMsgGCRequestStoreSalesData>(request.Body);
        var response = new CMsgGCRequestStoreSalesDataResponse
        {
            Version = requested.Version,
            ExpirationTime = (uint)_timeProvider.GetUtcNow().Add(Validity).ToUnixTimeSeconds()
        };

        return
        [
            new GcMessage(
                GcMsg.RequestStoreSalesDataResponse,
                context.Codec.Encode(response),
                TargetJobId: request.SourceJobId)
        ];
    }
}
