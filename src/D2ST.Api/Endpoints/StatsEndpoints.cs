using D2ST.Api.Contracts;
using D2ST.Core.Accounts;
using D2ST.Steam;
using D2ST.Steam.Stats;

namespace D2ST.Api.Endpoints;

public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stats/me", async (
            HttpContext http,
            ISessionStore sessions,
            IStatsService stats,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok((await stats.ReadAsync(session.Account.AccountId, cancellationToken)).ToApiStats());
        });

        app.MapPut("/api/stats/me", async (
            StoreStatsRequest request,
            HttpContext http,
            ISessionStore sessions,
            IStatsService stats,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            // The body carries a SteamId, but a client may only write its own
            // stats; a game server uses the /api/gameservers route instead.
            await stats.StoreAsync(
                session.Account.AccountId,
                request.Stats.ToStatValues(),
                request.Achievements.ToAchievementValues(),
                cancellationToken);
            return Results.Ok();
        });

        app.MapGet("/api/stats/users/{steamId:long}", async (
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

        return app;
    }
}
