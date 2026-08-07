using D2ST.Core.Accounts;
using D2ST.GameCoordinator.SharedObjects;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Lobbies;

/// <summary>
/// The practice lobbies players host and join. A lobby is a Shared Object
/// (<c>CSODOTALobby</c>, type 2004) on a cache owned by the lobby itself, the
/// same shape parties use: every member is subscribed to that one cache, so a
/// team change made by one player is drawn by all of them from a single delta.
/// <para>
/// The object is the state — membership, team slots, settings and the launch
/// state all live in it — and this service only keeps the indexes needed to find
/// a lobby from a Steam id. Lobbies are in memory: they exist while somebody is
/// in them and disappear with their host, so nothing about them is persisted.
/// </para>
/// </summary>
public sealed class LobbyService : IGcWelcomeContributor
{
    /// <summary>Slots per playing team: five Radiant and five Dire.</summary>
    public const uint SlotsPerTeam = 5;

    private const int SequenceBits = 20;
    private const ulong SequenceMask = (1UL << SequenceBits) - 1;

    private readonly SoCacheService _soCache;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Dictionary<ulong, ulong> _memberships = [];
    private ulong _sequence;

    public LobbyService(SoCacheService soCache, TimeProvider time)
    {
        _soCache = soCache;
        _time = time;
    }

    /// <summary>The lobby a player is in, or null. Read-only snapshot.</summary>
    public CSODOTALobby? Find(ulong steamId)
    {
        lock (_gate)
        {
            return TryGetLobbyOf(steamId, out var lobby) ? lobby : null;
        }
    }

    /// <summary>
    /// Hosts a new lobby. A player can only be in one, so an existing lobby is
    /// left first — the client sends a create straight from a lobby it is
    /// already sitting in when the host changes game mode from the menu.
    /// </summary>
    public DOTAJoinLobbyResult Create(GcContext context, CMsgPracticeLobbyCreate request)
    {
        lock (_gate)
        {
            if (TryGetLobbyOf(context.SteamId, out var current))
            {
                Detach(current, context.SteamId);
            }

            var lobby = new CSODOTALobby
            {
                LobbyId = NextId(),
                LeaderId = context.SteamId,
                lobby_type = CSODOTALobby.LobbyType.Practice,
                state = CSODOTALobby.State.Ui,
                PassKey = request.PassKey,
                Visibility = DOTALobbyVisibility.DOTALobbyVisibilityPublic
            };

            ApplyDetails(lobby, request.LobbyDetails);
            AddMember(lobby, context, DotaGcTeam.DotaGcTeamGoodGuys);
            Write(lobby);
            _soCache.PushSubscribe(context.AccountId, SoOwner.ForLobby(lobby.LobbyId));
            return DOTAJoinLobbyResult.DotaJoinResultSuccess;
        }
    }

    /// <summary>
    /// Joins an existing lobby. The player lands in the player pool when both
    /// teams are full, which is what the client draws as "unassigned".
    /// </summary>
    public DOTAJoinLobbyResult Join(GcContext context, CMsgPracticeLobbyJoin request)
    {
        lock (_gate)
        {
            if (!TryGetLobby(request.LobbyId, out var lobby))
            {
                return DOTAJoinLobbyResult.DotaJoinResultInvalidLobby;
            }

            if (lobby.Members.Any(member => member.Id == context.SteamId))
            {
                return DOTAJoinLobbyResult.DotaJoinResultSuccess;
            }

            if (!string.IsNullOrEmpty(lobby.PassKey) && lobby.PassKey != request.PassKey)
            {
                return DOTAJoinLobbyResult.DotaJoinResultIncorrectPassword;
            }

            if (lobby.state != CSODOTALobby.State.Ui)
            {
                return DOTAJoinLobbyResult.DotaJoinResultAlreadyInGame;
            }

            if (TryGetLobbyOf(context.SteamId, out var current))
            {
                Detach(current, context.SteamId);
                if (!TryGetLobby(request.LobbyId, out lobby))
                {
                    return DOTAJoinLobbyResult.DotaJoinResultInvalidLobby;
                }
            }

            AddMember(lobby, context, FreeTeam(lobby));
            Write(lobby);
            _soCache.PushSubscribe(context.AccountId, SoOwner.ForLobby(lobby.LobbyId));
            return DOTAJoinLobbyResult.DotaJoinResultSuccess;
        }
    }

    public void Leave(GcContext context)
    {
        lock (_gate)
        {
            if (TryGetLobbyOf(context.SteamId, out var lobby))
            {
                Detach(lobby, context.SteamId);
            }
        }
    }

    /// <summary>Removes another member. Only the host may, and never itself.</summary>
    public void Kick(GcContext context, uint accountId)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby) || lobby.LeaderId != context.SteamId)
            {
                return;
            }

            var target = lobby.Members.FirstOrDefault(member => AccountIdOf(member.Id) == accountId);
            if (target is null || target.Id == context.SteamId)
            {
                return;
            }

            Detach(lobby, target.Id);
        }
    }

    /// <summary>
    /// Moves a member out of its team and into the player pool. Unlike a kick it
    /// leaves the player in the lobby, which is how the host frees a slot.
    /// </summary>
    public void KickFromTeam(GcContext context, uint accountId)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby) || lobby.LeaderId != context.SteamId)
            {
                return;
            }

            var target = lobby.Members.FirstOrDefault(member => AccountIdOf(member.Id) == accountId);
            if (target is null || target.Team == DotaGcTeam.DotaGcTeamPlayerPool)
            {
                return;
            }

            target.Team = DotaGcTeam.DotaGcTeamPlayerPool;
            target.Slot = 0;
            Write(lobby);
        }
    }

    /// <summary>Applies the host's settings. Only the host may, and not once the game is running.</summary>
    public void SetDetails(GcContext context, CMsgPracticeLobbySetDetails details)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby) ||
                lobby.LeaderId != context.SteamId ||
                lobby.state != CSODOTALobby.State.Ui)
            {
                return;
            }

            ApplyDetails(lobby, details);
            Write(lobby);
        }
    }

    /// <summary>
    /// Puts the caller in a team slot. A slot somebody else holds is refused
    /// rather than shared, so two players cannot end up on the same one.
    /// </summary>
    public void SetTeamSlot(GcContext context, CMsgPracticeLobbySetTeamSlot request)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby) || lobby.state != CSODOTALobby.State.Ui)
            {
                return;
            }

            var member = lobby.Members.FirstOrDefault(entry => entry.Id == context.SteamId);
            if (member is null)
            {
                return;
            }

            var team = request.Team;
            if (IsPlayingTeam(team))
            {
                var slot = request.Slot is >= 1 and <= SlotsPerTeam ? request.Slot : FreeSlot(lobby, team);
                if (slot == 0 || lobby.Members.Any(other =>
                        other.Id != member.Id && other.Team == team && other.Slot == slot))
                {
                    return;
                }

                member.Team = team;
                member.Slot = slot;
            }
            else
            {
                member.Team = team;
                member.Slot = 0;
            }

            Write(lobby);
        }
    }

    /// <summary>
    /// Starts the game. There is no game server to hand the lobby to, so the
    /// lobby moves to <c>SERVERSETUP</c> and stays there: the members see the
    /// launch, and nothing pretends a match exists.
    /// </summary>
    public void Launch(GcContext context)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby) ||
                lobby.LeaderId != context.SteamId ||
                lobby.state != CSODOTALobby.State.Ui)
            {
                return;
            }

            lobby.state = CSODOTALobby.State.Serversetup;
            Write(lobby);
        }
    }

    /// <summary>
    /// The lobbies the browser lists: public ones still in their UI state, minus
    /// any that asked for a different region or game mode than the request.
    /// </summary>
    public IReadOnlyList<CMsgPracticeLobbyListResponseEntry> List(CMsgPracticeLobbyList request)
    {
        lock (_gate)
        {
            return AllLobbies()
                .Where(lobby => lobby.state == CSODOTALobby.State.Ui)
                .Where(lobby => lobby.Visibility == DOTALobbyVisibility.DOTALobbyVisibilityPublic)
                .Where(lobby => request.Region == 0 || lobby.ServerRegion == request.Region)
                .Where(lobby => request.GameMode == DOTAGameMode.DotaGamemodeNone ||
                    lobby.GameMode == (uint)request.GameMode)
                .Select(ToListEntry)
                .ToList();
        }
    }

    /// <summary>The lobby cache a reconnecting client has to be resubscribed to.</summary>
    public IReadOnlyList<CMsgSOCacheSubscribed> CachesFor(GcContext context)
    {
        lock (_gate)
        {
            return TryGetLobbyOf(context.SteamId, out var lobby)
                ? _soCache.Subscribe(context.AccountId, SoOwner.ForLobby(lobby.LobbyId))
                : [];
        }
    }

    /// <summary>
    /// Removes a member, closing the lobby when it loses its host or its last
    /// player: a practice lobby belongs to whoever created it and the client
    /// offers no way to hand it over.
    /// </summary>
    private void Detach(CSODOTALobby lobby, ulong steamId)
    {
        var member = lobby.Members.FirstOrDefault(entry => entry.Id == steamId);
        if (member is null)
        {
            return;
        }

        if (lobby.LeaderId == steamId || lobby.Members.Count <= 1)
        {
            Close(lobby);
            return;
        }

        lobby.Members.Remove(member);
        _memberships.Remove(steamId);
        _soCache.Unsubscribe(AccountIdOf(steamId), SoOwner.ForLobby(lobby.LobbyId));
        Write(lobby);
    }

    private void Close(CSODOTALobby lobby)
    {
        foreach (var member in lobby.Members)
        {
            _memberships.Remove(member.Id);
        }

        _soCache.RemoveOwner(SoOwner.ForLobby(lobby.LobbyId));
    }

    private void AddMember(CSODOTALobby lobby, GcContext context, DotaGcTeam team)
    {
        lobby.Members.Add(new CDOTALobbyMember
        {
            Id = context.SteamId,
            Name = context.PersonaName,
            Team = team,
            Slot = IsPlayingTeam(team) ? FreeSlot(lobby, team) : 0
        });

        _memberships[context.SteamId] = lobby.LobbyId;
    }

    /// <summary>Radiant first, then Dire, then the pool — the order the client fills a lobby in.</summary>
    private static DotaGcTeam FreeTeam(CSODOTALobby lobby)
    {
        if (FreeSlot(lobby, DotaGcTeam.DotaGcTeamGoodGuys) != 0)
        {
            return DotaGcTeam.DotaGcTeamGoodGuys;
        }

        return FreeSlot(lobby, DotaGcTeam.DotaGcTeamBadGuys) != 0
            ? DotaGcTeam.DotaGcTeamBadGuys
            : DotaGcTeam.DotaGcTeamPlayerPool;
    }

    /// <summary>The lowest unused slot of a team, or 0 when it is full. Slots are 1-based.</summary>
    private static uint FreeSlot(CSODOTALobby lobby, DotaGcTeam team)
    {
        var taken = lobby.Members
            .Where(member => member.Team == team)
            .Select(member => member.Slot)
            .ToHashSet();

        for (var slot = 1u; slot <= SlotsPerTeam; slot++)
        {
            if (!taken.Contains(slot))
            {
                return slot;
            }
        }

        return 0;
    }

    private static bool IsPlayingTeam(DotaGcTeam team) =>
        team is DotaGcTeam.DotaGcTeamGoodGuys or DotaGcTeam.DotaGcTeamBadGuys;

    /// <summary>
    /// Copies the settings the client can change from the lobby screen. Fields
    /// the request leaves unset keep their current value, because the client
    /// sends the whole form back on every change.
    /// </summary>
    private static void ApplyDetails(CSODOTALobby lobby, CMsgPracticeLobbySetDetails? details)
    {
        if (details is null)
        {
            return;
        }

        if (details.ShouldSerializeGameName())
        {
            lobby.GameName = details.GameName;
        }

        if (details.ShouldSerializePassKey())
        {
            lobby.PassKey = details.PassKey;
        }

        if (details.ShouldSerializeServerRegion())
        {
            lobby.ServerRegion = details.ServerRegion;
        }

        if (details.ShouldSerializeGameMode())
        {
            lobby.GameMode = details.GameMode;
        }

        if (details.ShouldSerializeCmPick())
        {
            lobby.CmPick = details.CmPick;
        }

        if (details.ShouldSerializeAllowCheats())
        {
            lobby.AllowCheats = details.AllowCheats;
        }

        if (details.ShouldSerializeFillWithBots())
        {
            lobby.FillWithBots = details.FillWithBots;
        }

        if (details.ShouldSerializeIntroMode())
        {
            lobby.IntroMode = details.IntroMode;
        }

        if (details.ShouldSerializeAllowSpectating())
        {
            lobby.AllowSpectating = details.AllowSpectating;
        }

        if (details.ShouldSerializeVisibility())
        {
            lobby.Visibility = details.Visibility;
        }

        if (details.ShouldSerializeCustomGameMode())
        {
            lobby.CustomGameMode = details.CustomGameMode;
        }

        if (details.ShouldSerializeCustomMapName())
        {
            lobby.CustomMapName = details.CustomMapName;
        }

        if (details.ShouldSerializeCustomMaxPlayers())
        {
            lobby.CustomMaxPlayers = details.CustomMaxPlayers;
        }

        if (details.ShouldSerializeLan())
        {
            lobby.Lan = details.Lan;
        }
    }

    private static CMsgPracticeLobbyListResponseEntry ToListEntry(CSODOTALobby lobby)
    {
        var entry = new CMsgPracticeLobbyListResponseEntry
        {
            Id = lobby.LobbyId,
            Name = lobby.GameName,
            LeaderAccountId = AccountIdOf(lobby.LeaderId),
            RequiresPassKey = !string.IsNullOrEmpty(lobby.PassKey),
            GameMode = (DOTAGameMode)lobby.GameMode,
            ServerRegion = lobby.ServerRegion,
            CustomGameMode = lobby.CustomGameMode,
            CustomMapName = lobby.CustomMapName,
            MaxPlayerCount = lobby.CustomMaxPlayers != 0 ? lobby.CustomMaxPlayers : SlotsPerTeam * 2
        };

        foreach (var member in lobby.Members)
        {
            entry.Members.Add(new CMsgPracticeLobbyListResponseEntry.CLobbyMember
            {
                AccountId = AccountIdOf(member.Id),
                PlayerName = member.Name
            });
        }

        return entry;
    }

    private IEnumerable<CSODOTALobby> AllLobbies() =>
        _memberships.Values
            .Distinct()
            .Select(lobbyId => TryGetLobby(lobbyId, out var lobby) ? lobby : null)
            .OfType<CSODOTALobby>();

    private bool TryGetLobbyOf(ulong steamId, out CSODOTALobby lobby)
    {
        if (_memberships.TryGetValue(steamId, out var lobbyId) && TryGetLobby(lobbyId, out lobby))
        {
            return true;
        }

        _memberships.Remove(steamId);
        lobby = default!;
        return false;
    }

    private bool TryGetLobby(ulong lobbyId, out CSODOTALobby lobby) =>
        _soCache.TryGetObject(
            SoCacheKey.Lobby(lobbyId),
            new SoObjectKey(DotaSoCache.TypeDotaLobby, lobbyId),
            out lobby);

    private void Write(CSODOTALobby lobby) =>
        _soCache.Set(
            SoCacheKey.Lobby(lobby.LobbyId),
            new SoObjectKey(DotaSoCache.TypeDotaLobby, lobby.LobbyId),
            lobby);

    private static uint AccountIdOf(ulong steamId) => SteamAccount.AccountIdFromSteamId(steamId);

    /// <summary>Lobby ids in the same shape as party ids: the current second in the high bits, a counter in the low ones.</summary>
    private ulong NextId()
    {
        _sequence = (_sequence + 1) & SequenceMask;
        return ((ulong)_time.GetUtcNow().ToUnixTimeSeconds() << SequenceBits) | _sequence;
    }
}
