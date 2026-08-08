using D2ST.Core.GameCoordinator;
using D2ST.Core.Profiles;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Handlers;

/// <summary>
/// Loads a saved profile or mini-profile showcase. The requested account is
/// intentionally not restricted to the caller: this is the public read path
/// used when one client opens another user's profile.
/// </summary>
public sealed class ShowcaseGetUserDataHandler : IGcMessageHandler
{
    private readonly IShowcaseStore _showcases;

    public ShowcaseGetUserDataHandler(IShowcaseStore showcases)
    {
        _showcases = showcases;
    }

    public uint MessageType => GcMsg.ClientToGCShowcaseGetUserData;

    public IReadOnlyList<GcMessage> Handle(GcContext context, GcMessage request)
    {
        var requested = context.Codec.Decode<CMsgClientToGCShowcaseGetUserData>(request.Body);
        var showcaseType = ShowcaseTypes.Canonical((uint)requested.ShowcaseType);
        if (showcaseType == 0)
        {
            return [new GcMessage(
                GcMsg.ClientToGCShowcaseGetUserDataResponse,
                context.Codec.Encode(new CMsgClientToGCShowcaseGetUserDataResponse
                {
                    Response = CMsgClientToGCShowcaseGetUserDataResponse.EResponse.keUnknownShowcase
                }),
                TargetJobId: request.SourceJobId)];
        }

        var accountId = requested.AccountId != 0 ? requested.AccountId : context.AccountId;
        var saved = _showcases.Get(accountId, showcaseType);
        var showcase = saved is null
            ? new CMsgShowcase { ModerationState = CMsgShowcase.EModerationState.keModerationStateOk }
            : context.Codec.Decode<CMsgShowcase>(saved.Payload);
        var response = new CMsgClientToGCShowcaseGetUserDataResponse
        {
            Response = CMsgClientToGCShowcaseGetUserDataResponse.EResponse.keSuccess,
            Showcase = showcase
        };

        return
        [
            new GcMessage(GcMsg.ClientToGCShowcaseGetUserDataResponse,
                context.Codec.Encode(response), TargetJobId: request.SourceJobId)
        ];
    }
}
