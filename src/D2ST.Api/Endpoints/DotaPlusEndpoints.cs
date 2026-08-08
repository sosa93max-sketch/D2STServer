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
                : Results.Ok(ToResponse(
                    plus.Get(session.Account.AccountId),
                    plus.GetSnapshot(session.Account.AccountId)));
        });

        return app;
    }

    public static DotaPlusResponse ToResponse(
        DotaPlusState state,
        DotaPlusSnapshot? snapshot = null)
    {
        var now = DateTimeOffset.UtcNow;
        var active = state.IsActiveAt(now);
        return new DotaPlusResponse(
            state.AccountId,
            active,
            state.StartedAt,
            state.ExpiresAt,
            state.DaysRemainingAt(now),
            active ? 1u : 0u,
            snapshot?.Shards ?? 0,
            snapshot?.Challenges
                .Select(challenge => new DotaPlusChallengeResponse(
                    challenge.AccountId,
                    challenge.SlotId,
                    challenge.SequenceId,
                    challenge.TemplateId,
                    challenge.Completed,
                    challenge.Target,
                    challenge.IntParam1,
                    challenge.HeroId,
                    challenge.QuestRank,
                    challenge.MaxQuestRank,
                    challenge.CreatedAt))
                .ToArray() ?? []);
    }
}
