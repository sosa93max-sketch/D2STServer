namespace D2ST.Steam.Lobbies;

/// <summary>
/// A matchmaking search. Mirrors the filters the client stacks on
/// <c>ISteamMatchmaking::AddRequestLobby*Filter</c> before requesting a list.
/// </summary>
public sealed record LobbyQuery
{
    public uint AppId { get; init; }

    /// <summary>0 means "as many as the server wants to return".</summary>
    public int ResultCount { get; init; }

    /// <summary>Minimum number of free slots a lobby must still have.</summary>
    public int SlotsAvailable { get; init; }

    public IReadOnlyList<LobbyStringFilter> StringFilters { get; init; } = Array.Empty<LobbyStringFilter>();

    public IReadOnlyList<LobbyNumericalFilter> NumericalFilters { get; init; } = Array.Empty<LobbyNumericalFilter>();

    /// <summary>Sorting hints: results are ordered by distance to these values.</summary>
    public IReadOnlyList<LobbyNearValueFilter> NearValueFilters { get; init; } = Array.Empty<LobbyNearValueFilter>();
}

public sealed record LobbyStringFilter(string Key, string Value, LobbyComparison Comparison);

public sealed record LobbyNumericalFilter(string Key, int Value, LobbyComparison Comparison);

public sealed record LobbyNearValueFilter(string Key, int Value);

/// <summary>Steam's ELobbyComparison, kept at its wire values.</summary>
public enum LobbyComparison
{
    EqualToOrLessThan = -2,
    LessThan = -1,
    Equal = 0,
    GreaterThan = 1,
    EqualToOrGreaterThan = 2,
    NotEqual = 3
}
