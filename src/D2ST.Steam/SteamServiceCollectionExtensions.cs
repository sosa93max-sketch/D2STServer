using D2ST.Steam.Auth;
using D2ST.Steam.Events;
using D2ST.Steam.GameServers;
using D2ST.Steam.Invites;
using D2ST.Steam.Lobbies;
using D2ST.Steam.Networking;
using D2ST.Steam.Presence;
using D2ST.Steam.Leaderboards;
using D2ST.Steam.Social;
using D2ST.Steam.Stats;
using D2ST.Steam.Storage;
using D2ST.Steam.Workshop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D2ST.Steam;

public static class SteamServiceCollectionExtensions
{
    public static IServiceCollection AddSteamServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SteamOptions>(configuration.GetSection(SteamOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISessionStore, SessionStore>();
        services.AddSingleton<IPresenceTracker, PresenceTracker>();
        services.AddSingleton<IEventStream, EventStream>();
        services.AddSingleton<ILobbyService, LobbyService>();
        services.AddSingleton<IP2PRelay, P2PRelay>();
        services.AddSingleton<IGameInviteService, GameInviteService>();
        services.AddSingleton<IAuthTicketService, AuthTicketService>();
        services.AddSingleton<IGameServerRegistry, GameServerRegistry>();
        services.AddScoped<ISteamAuthService, SteamAuthService>();
        services.AddScoped<FriendGraph>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<SocialEventPublisher>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IRemoteStorageService, RemoteStorageService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IWorkshopService, WorkshopService>();
        services.AddHostedService<PresenceSweepService>();
        return services;
    }
}
