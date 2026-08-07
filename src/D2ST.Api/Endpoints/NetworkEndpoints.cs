using D2ST.Api.Contracts;
using D2ST.Steam;
using D2ST.Steam.Invites;
using D2ST.Steam.Networking;

namespace D2ST.Api.Endpoints;

/// <summary>
/// The relay the peers talk through, plus "join my game" invites: both are
/// pure pass-through routes whose payloads the server never interprets.
/// </summary>
public static class NetworkEndpoints
{
    public static IEndpointRouteBuilder MapNetworkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/network/p2p/send", (
            P2PPacketRequest request,
            HttpContext http,
            ISessionStore sessions,
            IP2PRelay relay) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            relay.Send(session, request.ToP2PPacket());
            return Results.Ok();
        });

        // The shim batches its outbound queue and only falls back to one call
        // per packet if this route fails, so it must accept the whole batch.
        app.MapPost("/api/network/p2p/send-batch", (
            P2PPacketBatchRequest request,
            HttpContext http,
            ISessionStore sessions,
            IP2PRelay relay) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            relay.Send(session, (request.Packets ?? Array.Empty<P2PPacketRequest>()).Select(packet => packet.ToP2PPacket()));
            return Results.Ok();
        });

        app.MapPost("/api/game-invites", (
            GameInviteRequest request,
            HttpContext http,
            ISessionStore sessions,
            IGameInviteService invites) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return invites.Invite(session, request.InviteeSteamId, request.ConnectString ?? string.Empty)
                ? Results.Ok()
                : Results.BadRequest();
        });

        return app;
    }
}
