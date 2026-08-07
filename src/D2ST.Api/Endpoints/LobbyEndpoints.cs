using D2ST.Api.Contracts;
using D2ST.Steam;
using D2ST.Steam.Lobbies;

namespace D2ST.Api.Endpoints;

/// <summary>
/// Matchmaking surface. Every route resolves the caller from its bearer token,
/// so a client can only act inside lobbies it is actually a member of.
/// </summary>
public static class LobbyEndpoints
{
    public static IEndpointRouteBuilder MapLobbyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/lobbies/query", (
            LobbyQueryRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
        {
            var session = http.Authenticate(sessions);
            return session is null
                ? Results.Unauthorized()
                : Results.Ok(lobbies.Query(request.ToLobbyQuery()).Select(lobby => lobby.ToApiLobby()).ToList());
        });

        app.MapPost("/api/lobbies", (
            CreateLobbyRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var lobby = lobbies.Create(
                session,
                request.AppId != 0 ? request.AppId : session.AppId,
                request.LobbyType,
                request.MaxMembers,
                request.LobbyData);

            return Results.Ok(lobby.ToApiLobby());
        });

        app.MapGet("/api/lobbies/{lobbyId}", (
            ulong lobbyId,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var lobby = lobbies.Find(lobbyId);
            return lobby is null ? Results.NotFound() : Results.Ok(lobby.ToApiLobby());
        });

        app.MapPost("/api/lobbies/{lobbyId}/join", (
            ulong lobbyId,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var lobby = lobbies.Join(session, lobbyId);
            return lobby is null ? Results.NotFound() : Results.Ok(lobby.ToApiLobby());
        });

        app.MapPost("/api/lobbies/{lobbyId}/leave", (
            ulong lobbyId,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
            Authorized(http, sessions, lobbies, (session, service) => service.Leave(session, lobbyId)));

        app.MapPost("/api/lobbies/{lobbyId}/invites", (
            ulong lobbyId,
            LobbyInviteRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
            Authorized(http, sessions, lobbies, (session, service) =>
                service.Invite(session, lobbyId, request.InviteeSteamId)));

        app.MapPut("/api/lobbies/{lobbyId}/data", (
            ulong lobbyId,
            LobbyDataUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
            Authorized(http, sessions, lobbies, (session, service) =>
                service.SetLobbyData(session, lobbyId, request.Key, request.Value)));

        app.MapPost("/api/lobbies/{lobbyId}/data/delete", (
            ulong lobbyId,
            LobbyDeleteDataRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
            Authorized(http, sessions, lobbies, (session, service) =>
                service.SetLobbyData(session, lobbyId, request.Key, value: null)));

        app.MapPut("/api/lobbies/{lobbyId}/member-data", (
            ulong lobbyId,
            LobbyDataUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
            Authorized(http, sessions, lobbies, (session, service) =>
                service.SetMemberData(session, lobbyId, request.Key, request.Value)));

        app.MapPut("/api/lobbies/{lobbyId}/gameserver", (
            ulong lobbyId,
            LobbyGameServerUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
            Authorized(http, sessions, lobbies, (session, service) =>
                service.SetGameServer(session, lobbyId, request.SteamIdGameServer, request.IP, request.Port)));

        app.MapPut("/api/lobbies/{lobbyId}/settings", (
            ulong lobbyId,
            LobbySettingsUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
            Authorized(http, sessions, lobbies, (session, service) => service.UpdateSettings(
                session,
                lobbyId,
                new LobbySettingsUpdate(request.Joinable, request.LobbyType, request.OwnerSteamId, request.MaxMembers))));

        app.MapPost("/api/lobbies/{lobbyId}/chat", (
            ulong lobbyId,
            LobbyChatRequest request,
            HttpContext http,
            ISessionStore sessions,
            ILobbyService lobbies) =>
            Authorized(http, sessions, lobbies, (session, service) =>
                service.SendChat(session, lobbyId, request.MessageBase64 ?? string.Empty)));

        return app;
    }

    private static IResult Authorized(
        HttpContext http,
        ISessionStore sessions,
        ILobbyService lobbies,
        Func<Core.Steam.SteamSession, ILobbyService, bool> action)
    {
        var session = http.Authenticate(sessions);
        if (session is null)
        {
            return Results.Unauthorized();
        }

        // A refused mutation means the lobby is gone or the caller is not
        // allowed to make it; the client treats both the same way.
        return action(session, lobbies) ? Results.Ok() : Results.BadRequest();
    }
}
