using D2ST.Api.Contracts;
using D2ST.Core.Accounts;
using D2ST.Steam;
using D2ST.Steam.Social;

namespace D2ST.Api.Endpoints;

public static class FriendEndpoints
{
    public static IEndpointRouteBuilder MapFriendEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/friends", async (
            HttpContext http,
            ISessionStore sessions,
            IUserDirectory users,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var friends = await users.ListFriendsAsync(session.Account.AccountId, ct);
            return Results.Ok(friends.Select(friend => friend.ToApiUser()).ToList());
        });

        app.MapPost("/api/friends/request", async (
            FriendActionRequest request,
            HttpContext http,
            ISessionStore sessions,
            IUserDirectory users,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var targetAccountId = request.SteamId != 0
                ? SteamAccount.AccountIdFromSteamId(request.SteamId)
                : await users.ResolveAccountIdAsync(request.Identifier ?? string.Empty, ct);

            return targetAccountId != 0 && await friends.RequestAsync(session.Account.AccountId, targetAccountId, ct)
                ? Results.Ok()
                : Results.BadRequest();
        });

        app.MapPost("/api/friends/{steamId:long}/accept", async (
            long steamId,
            HttpContext http,
            ISessionStore sessions,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return await friends.AcceptAsync(
                session.Account.AccountId,
                SteamAccount.AccountIdFromSteamId((ulong)steamId),
                ct)
                ? Results.Ok()
                : Results.BadRequest();
        });

        // Also used to decline an incoming invitation or withdraw an outgoing
        // one: from the client's side all three are "stop being connected".
        app.MapPost("/api/friends/{steamId:long}/remove", async (
            long steamId,
            HttpContext http,
            ISessionStore sessions,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return await friends.RemoveAsync(
                session.Account.AccountId,
                SteamAccount.AccountIdFromSteamId((ulong)steamId),
                ct)
                ? Results.Ok()
                : Results.BadRequest();
        });

        return app;
    }
}
