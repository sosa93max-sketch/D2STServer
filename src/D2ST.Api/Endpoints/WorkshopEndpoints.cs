using D2ST.Api.Contracts;
using D2ST.Steam;
using D2ST.Steam.Workshop;

namespace D2ST.Api.Endpoints;

/// <summary>
/// Workshop catalogue and per-account subscriptions. The server stores the
/// metadata only; the client already has the content on disk.
/// </summary>
public static class WorkshopEndpoints
{
    public static IEndpointRouteBuilder MapWorkshopEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workshop/subscriptions", async (
            HttpContext http,
            ISessionStore sessions,
            IWorkshopService workshop,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var subscriptions = await workshop.SubscriptionsAsync(session.Account.AccountId, cancellationToken);
            return Results.Ok(subscriptions.Select(subscription => subscription.ToApiSubscription()).ToList());
        });

        app.MapGet("/api/workshop/items/{publishedFileId:long}", async (
            long publishedFileId,
            HttpContext http,
            ISessionStore sessions,
            IWorkshopService workshop,
            CancellationToken cancellationToken) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            var item = await workshop.FindAsync((ulong)publishedFileId, cancellationToken);
            return item is null ? Results.NotFound() : Results.Ok(item.ToApiWorkshopItem());
        });

        app.MapPut("/api/workshop/items/{publishedFileId:long}", async (
            long publishedFileId,
            ApiWorkshopItem item,
            HttpContext http,
            ISessionStore sessions,
            IWorkshopService workshop,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var existing = await workshop.FindAsync((ulong)publishedFileId, cancellationToken);
            if (existing is not null && existing.OwnerSteamId != session.Account.SteamId)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var stored = await workshop.PutAsync(
                item.ToWorkshopItem((ulong)publishedFileId, session.Account.SteamId),
                cancellationToken);
            return Results.Ok(stored.ToApiWorkshopItem());
        });

        app.MapPost("/api/workshop/items/{publishedFileId:long}/subscribe", async (
            long publishedFileId,
            HttpContext http,
            ISessionStore sessions,
            IWorkshopService workshop,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var subscription = await workshop.SubscribeAsync(
                session.Account.AccountId,
                (ulong)publishedFileId,
                cancellationToken);
            return Results.Ok(new ApiWorkshopMutation(true, subscription?.ToApiSubscription()));
        });

        app.MapDelete("/api/workshop/items/{publishedFileId:long}/subscription", async (
            long publishedFileId,
            HttpContext http,
            ISessionStore sessions,
            IWorkshopService workshop,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var removed = await workshop.UnsubscribeAsync(
                session.Account.AccountId,
                (ulong)publishedFileId,
                cancellationToken);
            return Results.Ok(new ApiWorkshopMutation(removed, null));
        });

        return app;
    }
}
