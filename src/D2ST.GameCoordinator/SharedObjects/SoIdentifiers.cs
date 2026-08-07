using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.SharedObjects;

/// <summary>
/// Owner of a Shared Object cache: the <c>CMsgSOIDOwner</c> pair the client uses
/// to tell one cache from another (a Steam account, a party group, an invite).
/// </summary>
public readonly record struct SoOwner(uint Type, ulong Id)
{
    public static SoOwner ForSteamId(ulong steamId) => new((uint)DotaSoCache.OwnerTypeSteamId, steamId);

    public static SoOwner ForParty(ulong partyId) => new((uint)DotaSoCache.OwnerTypePartyGroup, partyId);

    public static SoOwner ForLobby(ulong lobbyId) => new((uint)DotaSoCache.OwnerTypeLobbyGroup, lobbyId);

    /// <summary>An invite owns a cache of its own, so it can be published to (and revoked from) exactly one player.</summary>
    public static SoOwner ForInvite(ulong inviteId) => new((uint)DotaSoCache.OwnerTypeInvite, inviteId);

    public CMsgSOIDOwner ToProto() => new() { Type = Type, Id = Id };
}

/// <summary>
/// One cache: an owner plus the service that publishes it (0 game, 1 econ). The
/// same owner has one cache per service and the client subscribes to each.
/// </summary>
public readonly record struct SoCacheKey(SoOwner Owner, uint ServiceId)
{
    public static SoCacheKey Game(ulong steamId) => new(SoOwner.ForSteamId(steamId), DotaSoCache.ServiceGame);

    public static SoCacheKey Econ(ulong steamId) => new(SoOwner.ForSteamId(steamId), DotaSoCache.ServiceEcon);

    public static SoCacheKey Party(ulong partyId) => new(SoOwner.ForParty(partyId), DotaSoCache.ServiceGame);

    public static SoCacheKey Lobby(ulong lobbyId) => new(SoOwner.ForLobby(lobbyId), DotaSoCache.ServiceGame);

    public static SoCacheKey PartyInvite(ulong inviteId) => new(SoOwner.ForInvite(inviteId), DotaSoCache.ServiceGame);
}

/// <summary>
/// Identity of one object inside a cache. <paramref name="Key"/> is the object's
/// own primary key (account id, item id, party id): a cache holds many objects of
/// the same type and updates have to address exactly one of them.
/// </summary>
public readonly record struct SoObjectKey(int TypeId, ulong Key)
{
    /// <summary>Key for a type the owner only ever has one of.</summary>
    public static SoObjectKey Singleton(int typeId) => new(typeId, 0);
}
