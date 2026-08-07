using System.Text.Json;
using D2ST.Api.Contracts;
using D2ST.Core.Accounts;
using D2ST.Core.Steam;
using D2ST.Persistence;
using D2ST.Steam;
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
            return Results.Ok(accounts.Select(account => new AdminUserResponse(
                account.AccountId,
                SteamAccount.SteamIdFromAccountId(account.AccountId),
                account.Username,
                account.PersonaName,
                online.Contains(account.AccountId),
                account.CreatedAt,
                account.Avatar is { Length: > 0 })).ToList());
        });

        app.MapPost("/api/admin/users", async (
            AdminCreateUserRequest request,
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

            var created = await auth.CreateUserAsync(
                request.Username, request.Password, request.PersonaName, ct);
            if (!created)
            {
                return Json(new AdminMessageResponse("El usuario ya existe o los datos son inválidos."), 409);
            }

            var account = await db.Accounts.AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Username == request.Username, ct);
            return Results.Ok(new AdminUserResponse(
                account!.AccountId,
                SteamAccount.SteamIdFromAccountId(account.AccountId),
                account.Username,
                account.PersonaName,
                false,
                account.CreatedAt,
                account.Avatar is { Length: > 0 }));
        });

        app.MapPost("/api/admin/users/{accountId:uint}/password", async (
            uint accountId,
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

            return await auth.SetPasswordAsync(accountId, request.Password, ct)
                ? Results.Ok(new AdminMessageResponse("Contraseña actualizada."))
                : Json(new AdminMessageResponse("Usuario no encontrado."), 404);
        });

        app.MapPatch("/api/admin/users/{accountId:uint}/persona", async (
            uint accountId,
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

            return await auth.SetPersonaAsync(accountId, request.PersonaName, ct)
                ? Results.Ok(new AdminMessageResponse("Persona actualizada."))
                : Json(new AdminMessageResponse("Usuario no encontrado."), 404);
        });

        app.MapDelete("/api/admin/users/{accountId:uint}", async (
            uint accountId,
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

            if (accountId == context.Session.Account.AccountId)
            {
                return Json(new AdminMessageResponse("No puedes eliminar tu propia cuenta."), 403);
            }

            var account = await db.Accounts
                .SingleOrDefaultAsync(entity => entity.AccountId == accountId, ct);
            if (account is null)
            {
                return Json(new AdminMessageResponse("Usuario no encontrado."), 404);
            }

            db.Friendships.RemoveRange(db.Friendships.Where(
                friendship => friendship.AccountId == accountId || friendship.FriendAccountId == accountId));
            db.FriendRequests.RemoveRange(db.FriendRequests.Where(
                request => request.FromAccountId == accountId || request.ToAccountId == accountId));
            db.RemoteStorageFiles.RemoveRange(db.RemoteStorageFiles.Where(file => file.AccountId == accountId));
            db.UserStats.RemoveRange(db.UserStats.Where(stat => stat.AccountId == accountId));
            db.UserAchievements.RemoveRange(db.UserAchievements.Where(achievement => achievement.AccountId == accountId));
            db.LeaderboardEntries.RemoveRange(db.LeaderboardEntries.Where(entry => entry.AccountId == accountId));
            db.WorkshopSubscriptions.RemoveRange(
                db.WorkshopSubscriptions.Where(subscription => subscription.AccountId == accountId));

            var owned = db.WorkshopItems.Where(
                item => item.OwnerSteamId == SteamAccount.SteamIdFromAccountId(accountId));
            db.WorkshopSubscriptions.RemoveRange(
                db.WorkshopSubscriptions.Where(subscription =>
                    owned.Any(item => item.PublishedFileId == subscription.PublishedFileId)));
            db.WorkshopItems.RemoveRange(owned);
            db.Accounts.Remove(account);
            await db.SaveChangesAsync(ct);
            sessions.RemoveAll(accountId);

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

    private sealed record AdminContext(SteamSession Session, bool IsAdmin);
}
