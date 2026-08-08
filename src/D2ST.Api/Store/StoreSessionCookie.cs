using Microsoft.AspNetCore.Http;

namespace D2ST.Api.Store;

/// <summary>
/// Cookie used only by the same-origin consumer store. The launcher never
/// gives the browser its long-lived bearer token; it gives the server a
/// single-use handoff code which is exchanged for this HttpOnly cookie.
/// </summary>
public static class StoreSessionCookie
{
    public const string Name = "d2st_store_session";
    public const string Path = "/api/store";

    public static CookieOptions ForSession(HttpContext http, DateTimeOffset expiresAt) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        Secure = http.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        MaxAge = expiresAt - DateTimeOffset.UtcNow
    };

    public static CookieOptions Expired(HttpContext http) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        Secure = http.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = DateTimeOffset.UnixEpoch,
        MaxAge = TimeSpan.Zero
    };
}
