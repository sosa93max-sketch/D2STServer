using D2ST.Core.GameCoordinator;
using D2ST.Core.Profiles;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Persists the caller's profile or mini-profile showcase and returns the
/// validated payload. Other clients load the same account's data through 8886.
/// </summary>
public sealed class ShowcaseSetUserDataHandler : IGcMessageHandler
{
    private const int MaxItems = 64;

    private readonly IShowcaseStore _showcases;

    public ShowcaseSetUserDataHandler(IShowcaseStore showcases)
    {
        _showcases = showcases;
    }

    public uint MessageType => GcMsg.ClientToGCShowcaseSetUserData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var submitted = context.Codec.Decode<CMsgClientToGCShowcaseSetUserData>(request.Body);
        var showcaseType = ShowcaseTypes.Canonical((uint)submitted.ShowcaseType);
        var showcase = submitted.Showcase;
        if (showcaseType == 0 || showcase is null || showcase.ShowcaseItems.Count > MaxItems)
        {
            var invalid = new CMsgClientToGCShowcaseSetUserDataResponse
            {
                Response = CMsgClientToGCShowcaseSetUserDataResponse.EResponse.keInternalError
            };
            return [new GcMessage(
                GcMsg.ClientToGCShowcaseSetUserDataResponse,
                context.Codec.Encode(invalid),
                TargetJobId: request.SourceJobId)];
        }

        // Moderation is local to this compatibility server. Do not preserve a
        // client-provided rejected state that could hide a profile from readers.
        showcase.ModerationState = CMsgShowcase.EModerationState.keModerationStateOk;
        _showcases.Set(
            context.AccountId,
            showcaseType,
            submitted.FormatVersion,
            context.Codec.Encode(showcase));

        var response = new CMsgClientToGCShowcaseSetUserDataResponse
        {
            Response = CMsgClientToGCShowcaseSetUserDataResponse.EResponse.keSuccess,
            ValidatedShowcase = showcase,
            LockedUntilTimestamp = 0
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCShowcaseSetUserDataResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
