using D2ST.Core.Steam;
using D2ST.Steam;
using Microsoft.Net.Http.Headers;

namespace D2ST.Api;

/// <summary>
/// Bearer-token lookup shared by every authenticated endpoint. Resolving the
/// session (instead of trusting the ids in the request body) is what stops one
/// logged-in client from acting as another account.
/// </summary>
public static class SessionAuthentication
{
    private const string Scheme = "Bearer ";

    /// <summary>
    /// Returns the session behind the request, refreshing its presence. Null
    /// means the caller must answer 401.
    /// </summary>
    public static SteamSession? Authenticate(this HttpContext http, ISessionStore sessions)
    {
        var header = http.Request.Headers[HeaderNames.Authorization].ToString();
        if (!header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var session = sessions.Find(header[Scheme.Length..].Trim());
        if (session is not null)
        {
            sessions.Touch(session);
        }

        return session;
    }
}
