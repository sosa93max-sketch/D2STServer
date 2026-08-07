using D2ST.Api.Contracts;
using D2ST.Core.Accounts;
using D2ST.Steam;
using D2ST.Steam.Auth;

namespace D2ST.Api.Endpoints;

/// <summary>
/// Session tickets. Everything here is scoped to the calling session, so a
/// client can only mint tickets for itself even though the request carries a
/// Steam id (the shim sends its game-server identity through the same route).
/// </summary>
public static class AuthTicketEndpoints
{
    public static IEndpointRouteBuilder MapAuthTicketEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/tickets/session", (
            AuthTicketRequest request,
            HttpContext http,
            ISessionStore sessions,
            IAuthTicketService tickets) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var ticket = tickets.Create(session, request.AppId, request.SteamId, request.GameServer);
            return Results.Ok(new AuthTicketResponse(
                ticket.Handle,
                Convert.ToBase64String(ticket.Ticket),
                (uint)ticket.Ticket.Length));
        });

        app.MapPost("/api/auth/tickets/encrypted", (
            EncryptedAppTicketRequest request,
            HttpContext http,
            ISessionStore sessions,
            IAuthTicketService tickets) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var ticket = tickets.CreateEncryptedAppTicket(session, request.AppId, Decode(request.UserDataBase64));
            return Results.Ok(new EncryptedAppTicketResponse(
                Core.Auth.TicketValidation.ResultOk,
                Convert.ToBase64String(ticket)));
        });

        app.MapPost("/api/auth/tickets/validate", (
            AuthValidateRequest request,
            HttpContext http,
            ISessionStore sessions,
            IAuthTicketService tickets) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            var validation = tickets.Validate(Decode(request.TicketBase64), request.SteamId, request.AppId);
            return Results.Ok(new AuthValidateResponse(
                validation.BeginAuthSessionResult,
                validation.AuthSessionResponse,
                validation.OwnerSteamId,
                validation.Success));
        });

        app.MapPost("/api/gameservers/users/connect", (
            ConnectAuthRequest request,
            HttpContext http,
            ISessionStore sessions,
            IAuthTicketService tickets) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            var result = tickets.ConnectAndAuthenticate(Decode(request.AuthBlobBase64), request.SteamId, request.AppId);
            return Results.Ok(new ConnectAuthResponse(
                result.Success,
                result.SteamId,
                result.OwnerSteamId,
                result.DenyReason,
                result.DenyMessage));
        });

        app.MapPost("/api/auth/tickets/end-session", (
            AuthEndSessionRequest request,
            HttpContext http,
            ISessionStore sessions,
            IAuthTicketService tickets) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            // Ending a session only invalidates the caller's own tickets;
            // otherwise any client could log everyone else off a server.
            var steamId = request.SteamId == 0 ? session.Account.SteamId : request.SteamId;
            if (SteamAccount.AccountIdFromSteamId(steamId) == session.Account.AccountId)
            {
                tickets.EndSession(steamId);
            }

            return Results.Ok();
        });

        app.MapPost("/api/auth/tickets/cancel", (
            CancelAuthTicketRequest request,
            HttpContext http,
            ISessionStore sessions,
            IAuthTicketService tickets) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            tickets.Cancel(request.Handle);
            return Results.Ok();
        });

        return app;
    }

    private static byte[] Decode(string? payload) =>
        string.IsNullOrEmpty(payload) ? [] : Convert.FromBase64String(payload);
}
