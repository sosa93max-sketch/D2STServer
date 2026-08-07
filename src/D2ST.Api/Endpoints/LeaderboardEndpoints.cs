using D2ST.Api.Contracts;
using D2ST.Steam;
using D2ST.Steam.Leaderboards;

namespace D2ST.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public static IEndpointRouteBuilder MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/leaderboards", async (
            LeaderboardFindRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILeaderboardService leaderboards,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest();
            }

            var leaderboard = await leaderboards.FindOrCreateAsync(
                session.AppId,
                request.Name,
                request.SortMethod,
                request.DisplayType,
                cancellationToken);
            return Results.Ok(leaderboard.ToApiLeaderboard());
        });

        app.MapGet("/api/leaderboards/{leaderboardId:long}", async (
            long leaderboardId,
            HttpContext http,
            ISessionStore sessions,
            ILeaderboardService leaderboards,
            CancellationToken cancellationToken) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            var leaderboard = await leaderboards.FindAsync((ulong)leaderboardId, cancellationToken);
            return leaderboard is null ? Results.NotFound() : Results.Ok(leaderboard.ToApiLeaderboard());
        });

        app.MapPost("/api/leaderboards/{leaderboardId:long}/entries", async (
            long leaderboardId,
            LeaderboardEntriesRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILeaderboardService leaderboards,
            CancellationToken cancellationToken) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            var entries = await leaderboards.EntriesAsync(
                (ulong)leaderboardId,
                request.RangeStart,
                request.RangeEnd,
                request.Users ?? Array.Empty<ulong>(),
                cancellationToken);
            return entries is null ? Results.NotFound() : Results.Ok(entries.ToApiEntries());
        });

        app.MapPut("/api/leaderboards/{leaderboardId:long}/score", async (
            long leaderboardId,
            LeaderboardScoreUploadRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILeaderboardService leaderboards,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var result = await leaderboards.UploadAsync(
                (ulong)leaderboardId,
                session.Account.AccountId,
                request.UploadMethod,
                request.Score,
                request.Details ?? Array.Empty<int>(),
                cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(new LeaderboardScoreUploadResponse(
                    result.Success,
                    result.ScoreChanged,
                    result.Score,
                    result.GlobalRankNew,
                    result.GlobalRankPrevious));
        });

        return app;
    }
}
