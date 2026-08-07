using D2ST.Api.Contracts;
using D2ST.Steam;
using D2ST.Steam.Storage;

namespace D2ST.Api.Endpoints;

/// <summary>Steam Cloud. Every route is scoped to the calling account's files.</summary>
public static class StorageEndpoints
{
    public static IEndpointRouteBuilder MapStorageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/storage/files", async (
            HttpContext http,
            ISessionStore sessions,
            IRemoteStorageService storage,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var files = await storage.ListAsync(session.Account.AccountId, cancellationToken);
            return Results.Ok(files.Select(file => file.ToApiFileListItem()).ToList());
        });

                // Catch-all: file names carry directory separators ("cfg/x.json").
        app.MapGet("/api/storage/files/{**fileName}", async (
            string fileName,
            HttpContext http,
            ISessionStore sessions,
            IRemoteStorageService storage,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            // ASP.NET leaves an escaped separator ("%2F") encoded in the path,
            // so the name has to be unescaped to match how it was stored.
            var file = await storage.FindAsync(
                session.Account.AccountId,
                Uri.UnescapeDataString(fileName),
                cancellationToken);
            // The shim treats 404 as "no cloud save yet", which is not an error.
            return file is null ? Results.NotFound() : Results.Ok(file.ToApiFile());
        });

        app.MapPut("/api/storage/files", async (
            RemoteStorageUploadRequest request,
            HttpContext http,
            ISessionStore sessions,
            IRemoteStorageService storage,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var content = string.IsNullOrEmpty(request.ContentBase64)
                ? []
                : Convert.FromBase64String(request.ContentBase64);

            var file = await storage.WriteAsync(
                session.Account.AccountId,
                request.FileName,
                content,
                request.SyncPlatforms,
                cancellationToken);

            return file is null ? Results.BadRequest() : Results.Ok(file.ToApiFile());
        });

        app.MapPost("/api/storage/files/delete", async (
            RemoteStorageFileNameRequest request,
            HttpContext http,
            ISessionStore sessions,
            IRemoteStorageService storage,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            return await storage.DeleteAsync(session.Account.AccountId, request.FileName, cancellationToken)
                ? Results.Ok()
                : Results.NotFound();
        });

        app.MapPost("/api/storage/files/share", async (
            RemoteStorageFileNameRequest request,
            HttpContext http,
            ISessionStore sessions,
            IRemoteStorageService storage,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var file = await storage.FindAsync(session.Account.AccountId, request.FileName, cancellationToken);
            if (file is null)
            {
                return Results.NotFound();
            }

            // No UGC hosting exists here, so the handle is just a stable id for
            // the file: enough for the client to keep referring to it.
            var handle = ((ulong)session.Account.AccountId << 32) ^ (uint)file.FileName.GetHashCode(StringComparison.Ordinal);
            return Results.Ok(new ApiRemoteStorageShare(handle, Core.Auth.TicketValidation.ResultOk));
        });

        app.MapGet("/api/storage/quota", async (
            HttpContext http,
            ISessionStore sessions,
            IRemoteStorageService storage,
            CancellationToken cancellationToken) =>
        {
            var session = http.Authenticate(sessions);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var quota = await storage.QuotaAsync(session.Account.AccountId, cancellationToken);
            return Results.Ok(new ApiRemoteStorageQuota(quota.TotalBytes, quota.AvailableBytes));
        });

        return app;
    }
}
