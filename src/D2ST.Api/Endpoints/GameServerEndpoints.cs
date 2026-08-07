using D2ST.Api.Contracts;
using D2ST.Core.Accounts;
using D2ST.Core.GameServers;
using D2ST.Steam;
using D2ST.Steam.GameServers;
using D2ST.Steam.Stats;

namespace D2ST.Api.Endpoints;

/// <summary>
/// The server browser directory and the stats routes a game server uses on
/// behalf of the players connected to it.
/// </summary>
public static class GameServerEndpoints
{
    public static IEndpointRouteBuilder MapGameServerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/gameservers/register", (
            GameServerStateRequest request,
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
            Register(request, http, sessions, registry));

        app.MapPost("/api/gameservers/logon", (
            GameServerStateRequest request,
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
            Register(request, http, sessions, registry));

        app.MapPut("/api/gameservers/state", (
            ApiGameServer server,
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            return registry.Update(server.ToGameServer()) ? Results.Ok() : Results.NotFound();
        });

        app.MapPost("/api/gameservers/heartbeat", (
            ApiGameServer server,
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            return registry.Heartbeat(server.ToGameServer()) ? Results.Ok() : Results.NotFound();
        });

        app.MapDelete("/api/gameservers/{steamId:long}", (
            long steamId,
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            return registry.LogOff((ulong)steamId) ? Results.Ok() : Results.NotFound();
        });

        app.MapGet("/api/gameservers", (
            uint appId,
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(registry.List(appId).Select(server => server.ToApiGameServer()).ToList());
        });

        app.MapGet("/api/gameservers/public-ip", (
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
            http.Authenticate(sessions) is null
                ? Results.Unauthorized()
                : Results.Ok(new GameServerPublicIpResponse(registry.PublicIp)));

        app.MapPut("/api/gameservers/users/data", (
            GameServerUserDataRequest request,
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            // The caller is the server, so the player belongs to whichever
            // server that session registered.
            var server = registry.FindByOwner(session.Account.AccountId);
            if (server is null)
            {
                return Results.NotFound();
            }

            registry.SetPlayer(
                server.SteamId,
                new GameServerPlayer(request.SteamId, request.PlayerName ?? string.Empty, (int)request.Score, 0f));
            return Results.Ok();
        });

        app.MapPost("/api/gameservers/users/disconnect", (
            DisconnectGameServerUserRequest request,
            HttpContext http,
            ISessionStore sessions,
            IGameServerRegistry registry) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var server = registry.FindByOwner(session.Account.AccountId);
            if (server is null)
            {
                return Results.NotFound();
            }

            registry.RemovePlayer(server.SteamId, request.SteamId);
            return Results.Ok();
        });

        app.MapGet("/api/gameservers/stats/users/{steamId:long}", async (
            long steamId,
            HttpContext http,
            ISessionStore sessions,
            IStatsService stats,
            CancellationToken cancellationToken) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            var read = await stats.ReadAsync(SteamAccount.AccountIdFromSteamId((ulong)steamId), cancellationToken);
            return Results.Ok(read.ToApiStats());
        });

        app.MapPut("/api/gameservers/stats/users/{steamId:long}", async (
            long steamId,
            StoreStatsRequest request,
            HttpContext http,
            ISessionStore sessions,
            IStatsService stats,
            CancellationToken cancellationToken) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            await stats.StoreAsync(
                SteamAccount.AccountIdFromSteamId((ulong)steamId),
                request.Stats.ToStatValues(),
                request.Achievements.ToAchievementValues(),
                cancellationToken);
            return Results.Ok();
        });

        return app;
    }

    private static IResult Register(
        GameServerStateRequest request,
        HttpContext http,
        ISessionStore sessions,
        IGameServerRegistry registry)
    {
        var session = http.Authenticate(sessions);
        if (session is null)
        {
            return Results.Unauthorized();
        }

        if (request.Server is null)
        {
            return Results.BadRequest();
        }

        return Results.Ok(registry.Register(request.Server.ToGameServer(), request.Anonymous, session.Account.AccountId));
    }
}
