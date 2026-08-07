using D2ST.Api.Contracts;
using D2ST.Core.Events;
using D2ST.Core.Steam;
using D2ST.Steam;
using D2ST.Steam.Presence;
using D2ST.Steam.Social;

namespace D2ST.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/version", () => Results.Ok(new VersionResponse(AppVersion.Current)));

        app.MapPost("/api/auth/login", async (LoginRequest request, ISteamAuthService auth, CancellationToken ct) =>
        {
            var session = await auth.LoginAsync(request.Username, request.Password, ct);
            return session is null
                ? Results.Unauthorized()
                : Results.Ok(new LoginResponse(session.Account.SteamId, session.Account.AccountId, session.Token));
        });

        app.MapPost("/api/auth/steam/session", async (
            SteamSessionRequest request,
            ISteamAuthService auth,
            IUserDirectory users,
            IPresenceTracker presence,
            SocialEventPublisher publisher,
            CancellationToken ct) =>
        {
            var session = await auth.CreateShimSessionAsync(
                new ShimLogon(
                    request.SteamId,
                    request.AccountId,
                    request.PersonaName,
                    request.AppId,
                    request.ClientInstanceId,
                    request.ProcessRole),
                ct);

            var accountId = session.Account.AccountId;
            if (session.ProcessRole == ProcessRoles.Client)
            {
                // Logon is the client's first statement of what it is running,
                // and friends must see it come online without waiting for the
                // presence sweep to notice the new session.
                presence.SetAppId(accountId, session.AppId);
                await publisher.PublishToAudienceAsync(
                    accountId,
                    SteamEventTypes.PersonaStateChanged,
                    PersonaChange.Name | PersonaChange.Status,
                    ct);
            }

            var profile = await users.FindAsync(accountId, accountId, ct)
                ?? throw new InvalidOperationException($"Account {accountId} is missing right after logon.");

            return Results.Ok(new SteamSessionResponse(session.Token, session.RefreshToken, profile.ToApiUser()));
        });

        return app;
    }
}
