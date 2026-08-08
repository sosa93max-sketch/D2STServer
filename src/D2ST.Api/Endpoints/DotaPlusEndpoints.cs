using D2ST.Api.Contracts;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.Steam;

namespace D2ST.Api.Endpoints;

public static class DotaPlusEndpoints
{
    public static IEndpointRouteBuilder MapDotaPlusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dota-plus", (
            HttpContext http,
            ISessionStore sessions,
            IDotaPlusStore plus) =>
        {
            var session = http.Authenticate(sessions);
            return session is null
                ? Results.Unauthorized()
                : Results.Ok(ToResponse(plus.Get(session.Account.AccountId)));
        });

        return app;
    }

    public static DotaPlusResponse ToResponse(DotaPlusState state)
    {
        var now = DateTimeOffset.UtcNow;
        var active = state.IsActiveAt(now);
        return new DotaPlusResponse(
            state.AccountId,
            active,
            state.StartedAt,
            state.ExpiresAt,
            state.DaysRemainingAt(now),
            active ? 1u : 0u);
    }
}
