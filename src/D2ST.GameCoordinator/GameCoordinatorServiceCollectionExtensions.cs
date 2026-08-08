using D2ST.GameCoordinator.Chat;
using D2ST.GameCoordinator.Diagnostics;
using D2ST.GameCoordinator.Econ;
using D2ST.GameCoordinator.Handlers;
using D2ST.GameCoordinator.Lobbies;
using D2ST.GameCoordinator.Messaging;
using D2ST.GameCoordinator.Matches;
using D2ST.GameCoordinator.Parties;
using D2ST.GameCoordinator.Players;
using D2ST.GameCoordinator.Profiles;
using D2ST.Core.Profiles;
using D2ST.GameCoordinator.SharedObjects;
using D2ST.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D2ST.GameCoordinator;

public static class GameCoordinatorServiceCollectionExtensions
{
    public static IServiceCollection AddGameCoordinator(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IGcMessageRecorder>(NullGcMessageRecorder.Instance);
        services.TryAddSingleton<IGcPlayerDirectory>(OfflineGcPlayerDirectory.Instance);
        services.AddSingleton<IGcProtoCodec, GcProtoCodec>();
        services.AddSingleton<GcMessageQueue>();
        services.TryAddSingleton<IGcMessageQueue>(provider => provider.GetRequiredService<GcMessageQueue>());
        services.AddSingleton<SoCacheStore>();
        services.AddSingleton<SoCacheService>();
        services.TryAddSingleton<IMatchStore, EmptyMatchStore>();
        services.TryAddSingleton<IProfileStore, EmptyProfileStore>();
        services.AddSingleton<WelcomeBuilder>();
        services.AddSingleton<ProfileCardBuilder>();
        services.AddSingleton<EconInventory>();
        services.AddSingleton<PartyService>();
        services.AddSingleton<IGcWelcomeContributor>(provider => provider.GetRequiredService<PartyService>());
        services.AddSingleton<LobbyService>();
        services.AddSingleton<IGcWelcomeContributor>(provider => provider.GetRequiredService<LobbyService>());
        services.AddSingleton<ChatService>();
        services.AddSingleton<IGcLogonListener>(provider => provider.GetRequiredService<ChatService>());
        services.AddSingleton<IGcMessageHandler, ClientHelloHandler>();
        services.AddSingleton<IGcMessageHandler, PingHandler>();
        services.AddSingleton<IGcMessageHandler, SoCacheSubscriptionRefreshHandler>();
        services.AddSingleton<IGcMessageHandler, GetProfileCardHandler>();
        services.AddSingleton<IGcMessageHandler, SetProfileCardSlotsHandler>();
        services.AddSingleton<IGcMessageHandler, LatestConductScorecardHandler>();
        services.AddSingleton<IGcMessageHandler, MatchmakingStatsHandler>();
        services.AddSingleton<IGcMessageHandler, StoreSalesDataHandler>();
        services.AddSingleton<IGcMessageHandler, WeekendTourneyScheduleHandler>();
        services.AddSingleton<IGcMessageHandler, MyTeamInfoHandler>();
        services.AddSingleton<IGcMessageHandler, EmoticonDataHandler>();
        services.AddSingleton<IGcMessageHandler, EventPointsHandler>();
        services.AddSingleton<IGcMessageHandler, RankRequestHandler>();
        services.AddSingleton<IGcMessageHandler, GetCurrentPrivateCoachingSessionHandler>();
        services.AddSingleton<IGcMessageHandler, NotificationsRequestHandler>();
        services.AddSingleton<IGcMessageHandler, RequestAccountGuildPersonaInfoHandler>();
        services.AddSingleton<IGcMessageHandler, ShowcaseGetUserDataHandler>();
        services.AddSingleton<IGcMessageHandler, RequestSocialFeedHandler>();
        services.AddSingleton<IGcMessageHandler, MatchesMinimalRequestHandler>();
        services.AddSingleton<IGcMessageHandler, CancelUnfinalizedTransactionsHandler>();
        services.AddSingleton<IGcMessageHandler, RequestGuildDataHandler>();
        services.AddSingleton<IGcMessageHandler, RequestGuildMembershipHandler>();
        services.AddSingleton<IGcMessageHandler, GetHeroStickersHandler>();
        services.AddSingleton<IGcMessageHandler, MonsterHunterGetUserDataHandler>();
        services.AddSingleton<IGcMessageHandler, GetQuestProgressHandler>();
        services.AddSingleton<IGcMessageHandler, AggregateMetricsHandler>();
        services.AddSingleton<IGcMessageHandler, GetAvailablePrivateCoachingSessionsSummaryHandler>();
        services.AddSingleton<IGcMessageHandler, ShowcaseSetUserDataHandler>();
        services.AddSingleton<IGcMessageHandler, ClaimEventActionHandler>();
        services.AddSingleton<IGcMessageHandler, LobbyListHandler>();
        services.AddSingleton<IGcMessageHandler, FriendPracticeLobbyListHandler>();
        services.AddSingleton<IGcMessageHandler, GetHeroStandingsHandler>();
        services.AddSingleton<IGcMessageHandler, GetAllHeroProgressHandler>();
        services.AddSingleton<IGcMessageHandler, GetAllHeroOrderHandler>();
        services.AddSingleton<IGcMessageHandler, GetPlayerMatchHistoryHandler>();
        services.AddSingleton<IGcMessageHandler, TeammateStatsHandler>();
        services.AddSingleton<IGcMessageHandler, GetTrophyListHandler>();
        services.AddSingleton<IGcMessageHandler, GetProfileTicketsHandler>();
        services.AddSingleton<IGcMessageHandler, RecalibrateMMRHandler>();
        services.AddSingleton<IGcMessageHandler, RequestEventPointLogV2Handler>();
        services.AddSingleton<IGcMessageHandler, FindTopSourceTVGamesHandler>();
        services.AddSingleton<IGcMessageHandler, TopLeagueMatchesHandler>();
        services.AddSingleton<IGcMessageHandler, TopFriendMatchesHandler>();
        services.AddSingleton<IGcMessageHandler, EquipItemsHandler>();
        services.AddSingleton<IGcMessageHandler, SetItemStyleHandler>();
        services.AddSingleton<IGcMessageHandler, UnlockItemStyleHandler>();
        services.AddSingleton<IGcMessageHandler, SetItemPositionsHandler>();
        services.AddSingleton<IGcMessageHandler, UseItemHandler>();
        services.AddSingleton<IGcMessageHandler, UnlockCrateHandler>();
        services.AddSingleton<IGcMessageHandler, UnpackBundleHandler>();
        services.AddSingleton<IGcMessageHandler, StorePurchaseInitHandler>();
        services.AddSingleton<IGcMessageHandler, StorePurchaseCancelHandler>();
        services.AddSingleton<IGcMessageHandler, RedeemItemHandler>();
        services.AddSingleton<IGcMessageHandler, PurchaseItemWithEventPointsHandler>();
        services.AddSingleton<IGcMessageHandler, InviteToPartyHandler>();
        services.AddSingleton<IGcMessageHandler, PartyInviteResponseHandler>();
        services.AddSingleton<IGcMessageHandler, LeavePartyHandler>();
        services.AddSingleton<IGcMessageHandler, KickFromPartyHandler>();
        services.AddSingleton<IGcMessageHandler, SetPartyLeaderHandler>();
        services.AddSingleton<IGcMessageHandler, CancelPartyInvitesHandler>();
        services.AddSingleton<IGcMessageHandler, PartyMemberSetCoachHandler>();
        services.AddSingleton<IGcMessageHandler, ClientPingDataHandler>();
        services.AddSingleton<IGcMessageHandler, PartyReadyCheckRequestHandler>();
        services.AddSingleton<IGcMessageHandler, PartyReadyCheckAcknowledgeHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbyCreateHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbyJoinHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbyLeaveHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbySetDetailsHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbySetTeamSlotHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbyKickHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbyKickFromTeamHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbyLaunchHandler>();
        services.AddSingleton<IGcMessageHandler, AbandonCurrentGameHandler>();
        services.AddSingleton<IGcMessageHandler, PracticeLobbyListHandler>();
        services.AddSingleton<IGcMessageHandler, GameServerHelloHandler>();
        services.AddSingleton<IGcMessageHandler, GameServerInfoHandler>();
        services.AddSingleton<IGcMessageHandler, LanServerAvailableHandler>();
        services.AddSingleton<IGcMessageHandler, ServerAvailableHandler>();
        services.AddSingleton<IGcMessageHandler, ConnectedPlayersHandler>();
        services.AddSingleton<IGcMessageHandler, PlayerFailedToConnectHandler>();
        services.AddSingleton<IGcMessageHandler, GameMatchSignOutHandler>();
        services.AddSingleton<IGcMessageHandler, BatchPlayerResourcesHandler>();
        services.AddSingleton<IGcMessageHandler, JoinChatChannelHandler>();
        services.AddSingleton<IGcMessageHandler, LeaveChatChannelHandler>();
        services.AddSingleton<IGcMessageHandler, ChatMessageHandler>();
        services.AddSingleton<IGcMessageHandler, RequestChatChannelListHandler>();
        services.AddSingleton<IGcMessageHandler, ChatGetMemberCountHandler>();
        services.AddSingleton<IGcMessageHandler, PrivateChatInviteHandler>();
        services.AddSingleton<IGcMessageHandler, PrivateChatKickHandler>();
        services.AddSingleton<IGcMessageHandler, PrivateChatPromoteHandler>();
        services.AddSingleton<IGcMessageHandler, PrivateChatDemoteHandler>();
        services.AddSingleton<GcRouter>();
        services.AddSingleton<GameCoordinatorService>();
        return services;
    }

    /// <summary>
    /// Registers the GC together with the diagnostics dump configured under
    /// <see cref="GcDiagnosticsOptions.SectionName"/>. Unhandled messages are
    /// written under <paramref name="contentRootPath"/>, which is how stage 4
    /// finds out what a build really asks for.
    /// </summary>
    public static IServiceCollection AddGameCoordinator(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        services.Configure<GcDiagnosticsOptions>(configuration.GetSection(GcDiagnosticsOptions.SectionName));
        services.Configure<GcChatOptions>(configuration.GetSection(GcChatOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IGcMessageRecorder>(provider =>
            ActivatorUtilities.CreateInstance<JsonlGcMessageRecorder>(provider, contentRootPath));
        return services.AddGameCoordinator();
    }
}
