namespace D2ST.Api;

/// <summary>
/// Server protocol/build version. The launcher compares its own version against
/// GET /api/version and refuses to connect on a mismatch.
/// </summary>
public static class AppVersion
{
    public const string Current = "0.1.0";
}
