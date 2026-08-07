namespace D2ST.Protocol.Dota;

/// <summary>
/// Shared Object cache identifiers. These are not in the .proto files: they are
/// the type/owner/service ids the GC and the client agree on out of band, so the
/// values are pinned here.
/// </summary>
public static class DotaSoCache
{
    /// <summary>Owner soid type for a cache keyed by a Steam account.</summary>
    public const int OwnerTypeSteamId = 1;

    /// <summary>
    /// Owner soid types for the caches shared by several players: a party group,
    /// a lobby group, and the one-object cache an invite lives in.
    /// </summary>
    public const int OwnerTypePartyGroup = 2;

    public const int OwnerTypeLobbyGroup = 3;

    public const int OwnerTypeInvite = 4;

    /// <summary>Service 0 carries game/account objects, service 1 econ objects.</summary>
    public const uint ServiceGame = 0;

    public const uint ServiceEcon = 1;

    // Econ (service 1) SO type ids.
    public const int TypeEconItem = 1;
    public const int TypeEconGameAccountClient = 7;

    // Dota (service 0) SO type ids.
    public const int TypeDotaGameAccountClient = 2002;
    public const int TypeDotaParty = 2003;
    public const int TypeDotaLobby = 2004;
    public const int TypeDotaPartyInvite = 2006;
    public const int TypeDotaPlayerChallenge = 2010;
    public const int TypeDotaGameAccountPlus = 2012;

    // The modern lobby cache carries five buckets: the lobby itself, the empty
    // lobby-invite bucket, the static lobby (names), the server lobby and the
    // server static lobby. Without them the 2026 client rejects the lobby
    // cache subscription and creating a lobby fails.
    public const int TypeDotaLobbyInviteBucket = 2013;
    public const int TypeDotaStaticLobby = 2014;
    public const int TypeDotaServerLobby = 2015;
    public const int TypeDotaServerStaticLobby = 2016;
}
