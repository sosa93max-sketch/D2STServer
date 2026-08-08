using System.Text.Json;
using D2ST.Api.Contracts;
using D2ST.Core.Accounts;
using D2ST.Core.Ranking;
using D2ST.Core.Steam;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.GameCoordinator.Lobbies;
using D2ST.GameCoordinator.Ranks;
using D2ST.Persistence;
using D2ST.Steam;
using D2ST.Steam.Social;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Endpoints;

/// <summary>
/// The user-administration surface: a small web page at /admin plus the JSON
/// endpoints behind it (list, create, rename, reset password, delete). Only
/// usernames listed in <c>Admin:Usernames</c> are allowed in; the admin account
/// is created like any other, on its first login.
/// </summary>
public static class AdminEndpoints
{
    // Results.Json uses the default serializer options, not the app's
    // PascalCase HttpJsonOptions, so error bodies carry the same naming.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };
    private const int MaxAvatarBytes = 2 * 1024 * 1024;
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin", (IHostEnvironment env) =>
        {
            var page = Path.Combine(env.ContentRootPath, "Admin", "admin.html");
            return File.Exists(page)
                ? Results.Content(File.ReadAllText(page), "text/html")
                : Results.NotFound("Admin page not deployed.");
        });

        app.MapGet("/api/admin/users", async (
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            IRankStore ranks,
            IDotaPlusStore plus,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            var accounts = await db.Accounts.AsNoTracking()
                .OrderBy(account => account.AccountId)
                .ToListAsync(ct);
            var online = sessions.OnlineAccounts();
            var accountIds = accounts.Select(account => account.AccountId).ToArray();
            var wallets = await db.Wallets.AsNoTracking()
                .Where(wallet => accountIds.Contains(wallet.AccountId))
                .ToDictionaryAsync(wallet => wallet.AccountId, ct);
            var plusStates = plus.GetMany(accountIds);
            var plusSnapshots = plus.GetManySnapshots(accountIds);
            return Results.Ok(accounts.Select(account =>
                ToResponse(
                    ranks,
                    account,
                    online.Contains(account.AccountId),
                    wallets.GetValueOrDefault(account.AccountId),
                    plusStates.GetValueOrDefault(account.AccountId),
                    plusSnapshots.GetValueOrDefault(account.AccountId))).ToList());
        });

        app.MapGet("/api/admin/users/page", async (
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            IRankStore ranks,
            IDotaPlusStore plus,
            int page = 1,
            int pageSize = 25,
            string? search = null,
            string status = "all",
            CancellationToken ct = default) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            var normalizedStatus = status.Trim().ToLowerInvariant();
            if (normalizedStatus is not ("all" or "online" or "offline"))
            {
                return Json(new AdminMessageResponse("El filtro de estado no es válido."), 400);
            }

            var online = sessions.OnlineAccounts().ToHashSet();
            var onlineIds = online.ToArray();
            var boundedPage = Math.Clamp(page, 1, 100_000);
            var boundedPageSize = Math.Clamp(pageSize, 10, 100);
            var query = db.Accounts.AsNoTracking();
            var normalizedSearch = search?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var hasNumericSearch = uint.TryParse(normalizedSearch, out var numericSearch);
                uint? steamSearchAccountId = null;
                if (ulong.TryParse(normalizedSearch, out var numericSteamId)
                    && numericSteamId >= SteamAccount.SteamIdBase)
                {
                    var candidateAccountId = SteamAccount.AccountIdFromSteamId(numericSteamId);
                    if (SteamAccount.SteamIdFromAccountId(candidateAccountId) == numericSteamId)
                    {
                        steamSearchAccountId = candidateAccountId;
                    }
                }

                query = hasNumericSearch
                    ? query.Where(account => account.AccountId == numericSearch
                        || (steamSearchAccountId.HasValue && account.AccountId == steamSearchAccountId.Value)
                        || account.Username.Contains(normalizedSearch)
                        || (account.PersonaName != null && account.PersonaName.Contains(normalizedSearch)))
                    : query.Where(account => account.Username.Contains(normalizedSearch)
                        || (account.PersonaName != null && account.PersonaName.Contains(normalizedSearch)));
            }

            if (normalizedStatus == "online")
            {
                query = onlineIds.Length == 0
                    ? query.Where(_ => false)
                    : query.Where(account => onlineIds.Contains(account.AccountId));
            }
            else if (normalizedStatus == "offline" && onlineIds.Length != 0)
            {
                query = query.Where(account => !onlineIds.Contains(account.AccountId));
            }

            var totalCount = await query.CountAsync(ct);
            var accounts = await query
                .OrderBy(account => account.AccountId)
                .Skip((boundedPage - 1) * boundedPageSize)
                .Take(boundedPageSize)
                .ToListAsync(ct);
            var accountIds = accounts.Select(account => account.AccountId).ToArray();
            var wallets = await db.Wallets.AsNoTracking()
                .Where(wallet => accountIds.Contains(wallet.AccountId))
                .ToDictionaryAsync(wallet => wallet.AccountId, ct);
            var plusStates = plus.GetMany(accountIds);
            var plusSnapshots = plus.GetManySnapshots(accountIds);
            var items = accounts.Select(account =>
                ToResponse(
                    ranks,
                    account,
                    online.Contains(account.AccountId),
                    wallets.GetValueOrDefault(account.AccountId),
                    plusStates.GetValueOrDefault(account.AccountId),
                    plusSnapshots.GetValueOrDefault(account.AccountId))).ToArray();

            return Results.Ok(new AdminUsersPageResponse(
                items,
                boundedPage,
                boundedPageSize,
                totalCount,
                online.Count));
        });

        app.MapPost("/api/admin/users", async (
            AdminCreateUserRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            ISteamAuthService auth,
            IRankStore ranks,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            var created = await auth.CreateUserAsync(
                request.Username, request.Password, request.PersonaName, ct);
            if (!created)
            {
                return Json(new AdminMessageResponse("El usuario ya existe o los datos son inválidos."), 409);
            }

            var account = await db.Accounts.AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Username == request.Username, ct);
            if (account is not null && account.Avatar is not { Length: > 0 })
            {
                var entity = await db.Accounts
                    .SingleOrDefaultAsync(entity => entity.AccountId == account.AccountId, ct);
                if (entity is not null)
                {
                    entity.Avatar = DefaultAvatar.Bytes;
                    await db.SaveChangesAsync(ct);
                    account = entity;
                }
            }

            return Results.Ok(ToResponse(ranks, account!, online: false));
        });

        app.MapPut("/api/admin/users/{accountId:long}/dota-plus", async (
            long accountId,
            AdminDotaPlusUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            IDotaPlusStore plus,
            DotaPlusProjection projection,
            LobbyService lobbies,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            if (accountId <= 0 || accountId > uint.MaxValue)
            {
                return Json(new AdminMessageResponse("Usuario no encontrado."), 404);
            }

            var result = plus.UpdateSubscription(
                (uint)accountId,
                request.Enabled,
                request.Days,
                extend: true,
                context.Session.Account.AccountId,
                request.Reason);
            var snapshot = plus.GetSnapshot((uint)accountId);
            var response = new AdminDotaPlusUpdateResponse(
                result.Success,
                result.Code,
                result.Message,
                DotaPlusEndpoints.ToResponse(result.State, snapshot));
            if (!result.Success)
            {
                return Json(response, result.Code == "account_not_found" ? 404 : 400);
            }

            projection.Refresh((uint)accountId);
            lobbies.RefreshDotaPlus((uint)accountId);
            return Results.Ok(response);
        });

        app.MapPut("/api/admin/users/{accountId:long}/dota-plus/shards", async (
            long accountId,
            AdminDotaPlusShardUpdateRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            IDotaPlusStore plus,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            if (accountId <= 0 || accountId > uint.MaxValue)
            {
                return Json(new AdminMessageResponse("Usuario no encontrado."), 404);
            }

            var result = plus.AdjustShards(
                (uint)accountId,
                request.Delta,
                context.Session.Account.AccountId,
                request.Reason);
            var state = plus.Get((uint)accountId);
            var response = new AdminDotaPlusShardUpdateResponse(
                result.Success,
                result.Code,
                result.Message,
                DotaPlusEndpoints.ToResponse(state, result.Snapshot));
            if (!result.Success)
            {
                return Json(response, result.Code == "account_not_found" ? 404 : 400);
            }

            return Results.Ok(response);
        });

        app.MapPut("/api/admin/users/{accountId:long}/avatar", async (
            long accountId,
            AdminSetAvatarRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            IUserDirectory users,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            if (!TryDecodeAvatar(request.ContentBase64, out var content))
            {
                return Json(new AdminMessageResponse("Imagen inválida: debe ser un PNG de hasta 2 MB."), 400);
            }

            return await users.SetAvatarAsync((uint)accountId, content, ct)
                ? Results.Ok(new AdminMessageResponse("Avatar actualizado."))
                : Json(new AdminMessageResponse("Usuario no encontrado."), 404);
        });

        app.MapPost("/api/admin/users/{accountId:long}/mmr/adjust", async (
            long accountId,
            AdminAdjustMmrRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            IRankStore ranks,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            var id = (uint)accountId;
            if (!await db.Accounts.AnyAsync(entity => entity.AccountId == id, ct))
            {
                return Json(new AdminMessageResponse("Usuario no encontrado."), 404);
            }

            var rank = ranks.Adjust(id, request.Delta);
            var info = RankMath.RankFor(rank.Mmr);
            return Results.Ok(new
            {
                Mmr = rank.Mmr,
                RankTier = info.Tier,
                RankStar = info.Star,
                RankValue = info.RankValue,
                RankProgress = info.ProgressPercent,
                IsCalibrated = rank.IsCalibrated,
                Message = $"MMR ajustado: {rank.Mmr}"
            });
        });

        app.MapPost("/api/admin/users/{accountId:long}/mmr/reset", async (
            long accountId,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            IRankStore ranks,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            var rank = ranks.Reset((uint)accountId);
            return Results.Ok(new AdminMessageResponse(
                $"MMR restablecido a {rank.Mmr} (sin calibrar)."));
        });

        app.MapPost("/api/admin/users/{accountId:long}/password", async (
            long accountId,
            AdminSetPasswordRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            ISteamAuthService auth,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            return await auth.SetPasswordAsync((uint)accountId, request.Password, ct)
                ? Results.Ok(new AdminMessageResponse("Contraseña actualizada."))
                : Json(new AdminMessageResponse("Usuario no encontrado."), 404);
        });

        app.MapPatch("/api/admin/users/{accountId:long}/persona", async (
            long accountId,
            AdminSetPersonaRequest request,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            ISteamAuthService auth,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            return await auth.SetPersonaAsync((uint)accountId, request.PersonaName, ct)
                ? Results.Ok(new AdminMessageResponse("Persona actualizada."))
                : Json(new AdminMessageResponse("Usuario no encontrado."), 404);
        });

        app.MapDelete("/api/admin/users/{accountId:long}", async (
            long accountId,
            HttpContext http,
            ISessionStore sessions,
            D2stDbContext db,
            IConfiguration config,
            CancellationToken ct) =>
        {
            var context = await AuthenticateAdminAsync(http, sessions, db, config, ct);
            if (context is null)
            {
                return Results.Unauthorized();
            }

            if (!context.IsAdmin)
            {
                return Forbidden();
            }

            var id = (uint)accountId;
            if (id == context.Session.Account.AccountId)
            {
                return Json(new AdminMessageResponse("No puedes eliminar tu propia cuenta."), 403);
            }

            var account = await db.Accounts
                .SingleOrDefaultAsync(entity => entity.AccountId == id, ct);
            if (account is null)
            {
                return Json(new AdminMessageResponse("Usuario no encontrado."), 404);
            }

            db.Friendships.RemoveRange(db.Friendships.Where(
                friendship => friendship.AccountId == id || friendship.FriendAccountId == id));
            db.FriendRequests.RemoveRange(db.FriendRequests.Where(
                request => request.FromAccountId == id || request.ToAccountId == id));
            db.RemoteStorageFiles.RemoveRange(db.RemoteStorageFiles.Where(file => file.AccountId == id));
            db.UserStats.RemoveRange(db.UserStats.Where(stat => stat.AccountId == id));
            db.UserAchievements.RemoveRange(db.UserAchievements.Where(achievement => achievement.AccountId == id));
            db.PlayerRanks.RemoveRange(db.PlayerRanks.Where(rank => rank.AccountId == id));
            db.LeaderboardEntries.RemoveRange(db.LeaderboardEntries.Where(entry => entry.AccountId == id));
            db.WorkshopSubscriptions.RemoveRange(
                db.WorkshopSubscriptions.Where(subscription => subscription.AccountId == id));

            var owned = db.WorkshopItems.Where(
                item => item.OwnerSteamId == SteamAccount.SteamIdFromAccountId(id));
            db.WorkshopSubscriptions.RemoveRange(
                db.WorkshopSubscriptions.Where(subscription =>
                    owned.Any(item => item.PublishedFileId == subscription.PublishedFileId)));
            db.WorkshopItems.RemoveRange(owned);
            db.WalletTransactions.RemoveRange(
                db.WalletTransactions.Where(transaction => transaction.AccountId == id));
            db.StorePurchaseTransactions.RemoveRange(
                db.StorePurchaseTransactions.Where(transaction => transaction.AccountId == id));
            db.EconItems.RemoveRange(db.EconItems.Where(item => item.AccountId == id));
            db.Wallets.RemoveRange(db.Wallets.Where(wallet => wallet.AccountId == id));
            db.DotaPlusTransactions.RemoveRange(
                db.DotaPlusTransactions.Where(transaction => transaction.AccountId == id));
            db.DotaPlusChallenges.RemoveRange(
                db.DotaPlusChallenges.Where(challenge => challenge.AccountId == id));
            db.DotaPlusShardTransactions.RemoveRange(
                db.DotaPlusShardTransactions.Where(transaction => transaction.AccountId == id));
            db.DotaPlusRelics.RemoveRange(
                db.DotaPlusRelics.Where(relic => relic.AccountId == id));
            db.DotaPlusAccounts.RemoveRange(
                db.DotaPlusAccounts.Where(subscription => subscription.AccountId == id));
            db.Accounts.Remove(account);
            await db.SaveChangesAsync(ct);
            sessions.RemoveAll(id);

            return Results.Ok(new AdminMessageResponse("Usuario eliminado."));
        });

        return app;
    }

    private static async Task<AdminContext?> AuthenticateAdminAsync(
        HttpContext http,
        ISessionStore sessions,
        D2stDbContext db,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var session = http.Authenticate(sessions);
        if (session is null)
        {
            return null;
        }

        var account = await db.Accounts.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.AccountId == session.Account.AccountId, cancellationToken);
        var admins = config.GetSection("Admin:Usernames").Get<List<string>>() ?? [];
        var isAdmin = account is not null &&
            admins.Contains(account.Username, StringComparer.OrdinalIgnoreCase);
        return new AdminContext(session, isAdmin);
    }

    private static IResult Forbidden() =>
        Json(new AdminMessageResponse("No tienes permisos de administrador."), 403);

    private static IResult Json(object value, int statusCode) =>
        Results.Json(value, JsonOptions, statusCode: statusCode);

    private static AdminUserResponse ToResponse(
        IRankStore ranks,
        AccountEntity account,
        bool online,
        WalletEntity? wallet = null,
        DotaPlusState? plus = null,
        DotaPlusSnapshot? plusSnapshot = null)
    {
        var rank = ranks.GetOrCreate(account.AccountId);
        var info = RankMath.RankFor(rank.Mmr);
        var now = DateTimeOffset.UtcNow;
        return new AdminUserResponse(
            account.AccountId,
            SteamAccount.SteamIdFromAccountId(account.AccountId).ToString(),
            account.Username,
            account.PersonaName,
            online,
            account.CreatedAt,
            account.Avatar is { Length: > 0 },
            rank.Mmr,
            info.Tier,
            info.Star,
            info.RankValue,
            info.ProgressPercent,
            rank.IsCalibrated,
            wallet?.BalanceDollars ?? 0,
            wallet?.ReservedDollars ?? 0,
            wallet is null ? 0 : Math.Max(0, wallet.BalanceDollars - wallet.ReservedDollars),
            plus?.IsActiveAt(now) ?? false,
            plus?.ExpiresAt,
            plus?.DaysRemainingAt(now) ?? 0,
            plusSnapshot?.Shards ?? 0);
    }

    private static bool TryDecodeAvatar(string? contentBase64, out byte[] content)
    {
        content = [];
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
        return content.Length <= MaxAvatarBytes && content.AsSpan().StartsWith(PngSignature);
    }

    private sealed record AdminContext(SteamSession Session, bool IsAdmin);
}
