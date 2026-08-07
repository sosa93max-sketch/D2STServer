namespace D2ST.Core.Steam;

/// <summary>
/// Role of the process behind a session. Only a "client" is a player: a
/// dedicated server logs on with the same endpoints but must never show up as
/// online presence, and it receives its own events.
/// </summary>
public static class ProcessRoles
{
    public const string Client = "client";
    public const string Dedicated = "dedicated";

    public static string Normalize(string? processRole) => processRole?.Trim().ToLowerInvariant() switch
    {
        "dedicated" or "server" => Dedicated,
        _ => Client
    };
}
