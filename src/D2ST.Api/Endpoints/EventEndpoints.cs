using D2ST.Api.Contracts;
using D2ST.Steam;
using D2ST.Steam.Events;

namespace D2ST.Api.Endpoints;

/// <summary>
/// The client's only inbound channel. It long-polls this endpoint and replays
/// each event as the Steamworks callback the game expects.
/// </summary>
public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", async (
            string? since,
            int? waitMs,
            HttpContext http,
            ISessionStore sessions,
            IEventStream events,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            // An unparsable or missing cursor means "everything you have":
            // a client that just started has no cursor yet.
            _ = long.TryParse(since, out var cursor);

            var batch = await events.ReadAsync(
                session,
                cursor,
                TimeSpan.FromMilliseconds(Math.Max(0, waitMs ?? 0)),
                ct);

            return Results.Ok(new ApiEventEnvelope(
                batch.Cursor.ToString(),
                batch.Events.Select(steamEvent => steamEvent.ToApiEvent()).ToList()));
        });

        return app;
    }
}
