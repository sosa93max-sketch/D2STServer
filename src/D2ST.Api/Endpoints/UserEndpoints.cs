using D2ST.Api.Contracts;
using D2ST.Core.Accounts;
using D2ST.Core.Events;
using D2ST.Core.Ranking;
using D2ST.Core.Steam;
using D2ST.GameCoordinator.Ranks;
using D2ST.Persistence;
using D2ST.Steam;
using D2ST.Steam.Presence;
using D2ST.Steam.Social;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Endpoints;

/// <summary>
/// Identity, presence and avatars: everything the client reads to render a
/// player, plus the writes it performs on itself.
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/me", async (
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

            var accountId = session.Account.AccountId;
            var profile = await users.FindAsync(accountId, accountId, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile.ToApiUser());
        });

        app.MapGet("/api/users/me/rank", (
            HttpContext http,
            ISessionStore sessions,
            IRankStore ranks) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var rank = ranks.GetOrCreate(session.Account.AccountId);
            var info = RankMath.VisibleRankFor(rank);
            return Results.Ok(new
            {
                AccountId = rank.AccountId,
                Mmr = rank.Mmr,
                RankTier = info.Tier,
                RankStar = info.Star,
                RankValue = info.RankValue,
                RankProgress = info.ProgressPercent,
                IsCalibrated = rank.IsCalibrated
            });
        });

        app.MapGet("/api/ranking", async (
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            var requestedPage = Math.Clamp(page ?? 1, 1, 10_000);
            var requestedPageSize = Math.Clamp(pageSize ?? 50, 10, 100);
            var rankedPlayers = db.PlayerRanks
                .AsNoTracking()
                .Where(rank => rank.IsCalibrated)
                .Join(
                    db.Accounts.AsNoTracking(),
                    rank => rank.AccountId,
                    account => account.AccountId,
                    (rank, account) => new { Rank = rank, Account = account });

            var totalCount = await rankedPlayers.CountAsync(ct);
            var rows = await rankedPlayers
                .OrderByDescending(entry => entry.Rank.Mmr)
                .ThenByDescending(entry => entry.Rank.Wins)
                .ThenByDescending(entry => entry.Rank.Games)
                .ThenBy(entry => entry.Rank.AccountId)
                .Skip((requestedPage - 1) * requestedPageSize)
                .Take(requestedPageSize)
                .ToListAsync(ct);

            var items = rows.Select((entry, index) =>
            {
                var info = RankMath.RankFor(entry.Rank.Mmr);
                var games = Math.Max(0, entry.Rank.Games);
                var wins = Math.Clamp(entry.Rank.Wins, 0, games);
                var losses = Math.Clamp(entry.Rank.Losses, 0, games);
                var winRate = games == 0 ? 0 : (int)Math.Round(wins * 100.0 / games);
                return new RankingEntryResponse(
                    Position: ((requestedPage - 1) * requestedPageSize) + index + 1,
                    AccountId: entry.Account.AccountId,
                    SteamId: SteamAccount.SteamIdFromAccountId(entry.Account.AccountId).ToString(),
                    Username: entry.Account.Username,
                    PersonaName: entry.Account.PersonaName ?? entry.Account.Username,
                    Online: sessions.IsOnline(entry.Account.AccountId),
                    Mmr: Math.Max(0, entry.Rank.Mmr),
                    RankTier: info.Tier,
                    RankStar: info.Star,
                    RankValue: info.RankValue,
                    RankProgress: info.ProgressPercent,
                    IsCalibrated: entry.Rank.IsCalibrated,
                    Games: games,
                    Wins: wins,
                    Losses: losses,
                    WinRatePercent: winRate);
            }).ToList();

            return Results.Ok(new RankingPageResponse(
                items,
                requestedPage,
                requestedPageSize,
                totalCount));
        });

        app.MapGet("/api/users", async (
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

            var profiles = await users.ListAllAsync(session.Account.AccountId, ct);
            return Results.Ok(profiles.Select(profile => profile.ToApiUser()).ToList());
        });

        app.MapGet("/api/users/{steamId:long}", async (
            long steamId,
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

            var profile = await users.FindAsync(
                session.Account.AccountId,
                SteamAccount.AccountIdFromSteamId((ulong)steamId),
                ct);

            return profile is null ? Results.NotFound() : Results.Ok(profile.ToApiUser());
        });

        app.MapMethods("/api/users/me/persona", new[] { "PATCH" }, async (
            PersonaUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            IUserDirectory users,
            SocialEventPublisher publisher,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var accountId = session.Account.AccountId;
            var profile = await users.SetPersonaNameAsync(accountId, request.PersonaName, ct);
            if (profile is null)
            {
                return Results.NotFound();
            }

            session.PersonaName = profile.PersonaName;
            await publisher.PublishToAudienceAsync(
                accountId,
                SteamEventTypes.PersonaStateChanged,
                PersonaChange.Name,
                ct);

            return Results.Ok(profile.ToApiUser());
        });

        app.MapGet("/api/users/{steamId:long}/avatar", async (
            long steamId,
            HttpContext http,
            ISessionStore sessions,
            IUserDirectory users,
            CancellationToken ct) =>
        {
            if (http.Authenticate(sessions) is null)
            {
                return Results.Unauthorized();
            }

            var avatar = await users.GetAvatarAsync(SteamAccount.AccountIdFromSteamId((ulong)steamId), ct);

            // The client caches avatars by Steam id and refuses a response whose
            // identity header does not match what it asked for, so echo it back.
            http.Response.Headers.CacheControl = "no-cache, must-revalidate";
            http.Response.Headers.ETag = $"\"{avatar.ETag}\"";
            http.Response.Headers[AvatarSteamIdHeader] = avatar.SteamId.ToString();
            http.Response.Headers[AvatarDefaultHeader] = avatar.IsDefault ? "true" : "false";
            return Results.File(avatar.Content, "image/png");
        });

        app.MapPut("/api/users/me/avatar", async (
            AvatarUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            IUserDirectory users,
            SocialEventPublisher publisher,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            if (!TryDecode(request.ContentBase64, out var content))
            {
                return Results.BadRequest();
            }

            var accountId = session.Account.AccountId;
            if (!await users.SetAvatarAsync(accountId, content, ct))
            {
                return Results.BadRequest();
            }

            await publisher.PublishToAudienceAsync(
                accountId,
                SteamEventTypes.PersonaStateChanged,
                PersonaChange.Avatar,
                ct);

            return Results.Ok();
        });

        return app.MapPresenceEndpoints();
    }

    private const string AvatarSteamIdHeader = "X-SKYNET-Avatar-SteamId";
    private const string AvatarDefaultHeader = "X-SKYNET-Avatar-Default";

    private static IEndpointRouteBuilder MapPresenceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/presence", async (
            PresenceUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            IPresenceTracker presence,
            SocialEventPublisher publisher,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var accountId = session.Account.AccountId;
            presence.SetRichPresence(accountId, request.Key, request.Value);
            await publisher.PublishToAudienceAsync(
                accountId,
                SteamEventTypes.FriendPresenceChanged,
                PersonaChange.RichPresence,
                ct);

            return Results.Ok();
        });

        app.MapPut("/api/presence/game-server", async (
            GameServerPresenceUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            IPresenceTracker presence,
            SocialEventPublisher publisher,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var accountId = session.Account.AccountId;
            presence.SetGameServer(accountId, request.SteamId, request.Ip, request.Port);
            await publisher.PublishToAudienceAsync(
                accountId,
                SteamEventTypes.FriendPresenceChanged,
                PersonaChange.RichPresence,
                ct);

            return Results.Ok();
        });

        app.MapPost("/api/presence/offline", async (
            HttpContext http,
            ISessionStore sessions,
            IPresenceTracker presence,
            SocialEventPublisher publisher,
            CancellationToken ct) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            // The game is shutting down: drop every client session of the
            // account so friends see it offline now instead of when the
            // presence window lapses.
            var accountId = session.Account.AccountId;
            // Revoke the launcher's bearer token so every native API surface
            // stops working as soon as the launcher logs out.
            sessions.Remove(session.Token);
            sessions.RemoveClientSessions(accountId);
            presence.Clear(accountId);
            await publisher.PublishToAudienceAsync(
                accountId,
                SteamEventTypes.PersonaStateChanged,
                PersonaChange.Status,
                ct);

            return Results.Ok();
        });

        return app;
    }

    private static bool TryDecode(string? contentBase64, out byte[] content)
    {
        content = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            return false;
        }

        var buffer = new byte[(contentBase64.Length * 3 / 4) + 3];
        if (!Convert.TryFromBase64String(contentBase64, buffer, out var written) || written == 0)
        {
            return false;
        }

        content = buffer[..written];
        return true;
    }
}
