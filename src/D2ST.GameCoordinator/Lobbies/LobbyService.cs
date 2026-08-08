using D2ST.Core.Accounts;
using D2ST.Core.Ranking;
using D2ST.GameCoordinator.Ranks;
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
    private const uint DefaultServerPort = 27015;

    private readonly SoCacheService _soCache;
    private readonly IRankStore _ranks;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Dictionary<ulong, ulong> _memberships = [];
    private readonly Dictionary<ulong, LobbyServer> _servers = [];
    private readonly Dictionary<ulong, ulong> _serversBySteamId = [];
    private readonly Dictionary<ulong, Dictionary<ulong, string>> _memberNames = [];
    private ulong _sequence;

    public LobbyService(SoCacheService soCache, IRankStore ranks, TimeProvider time)
    {
        _soCache = soCache;
        _ranks = ranks;
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

    /// <summary>The lobby a game server is attached to, or null.</summary>
    public CSODOTALobby? FindByServer(ulong steamId)
    {
        lock (_gate)
        {
            return LobbyOfServer(steamId);
        }
    }

    /// <summary>Writes a lobby state change out to its subscribers.</summary>
    public void Publish(CSODOTALobby lobby)
    {
        lock (_gate)
        {
            Write(lobby);
        }
    }

    /// <summary>
    /// Applies the authoritative result received from the game server to the
    /// lobby Shared Object. Keeping this transition here ensures the main and
    /// auxiliary lobby caches are updated together.
    /// </summary>
    public void CompleteMatch(
        ulong serverSteamId,
        ulong matchId,
        DotaGcTeam winningTeam,
        uint firstBloodTime)
    {
        lock (_gate)
        {
            var lobby = LobbyOfServer(serverSteamId);
            if (lobby is null)
            {
                return;
            }

            if (matchId != 0)
            {
                lobby.MatchId = matchId;
            }

            lobby.state = CSODOTALobby.State.Postgame;
            lobby.GameState = DOTAGameState.DotaGamerulesStatePostGame;
            lobby.FirstBloodHappened = firstBloodTime != 0;
            lobby.MatchOutcome = winningTeam switch
            {
                DotaGcTeam.DotaGcTeamGoodGuys => EMatchOutcome.kEMatchOutcomeRadVictory,
                DotaGcTeam.DotaGcTeamBadGuys => EMatchOutcome.kEMatchOutcomeDireVictory,
                _ => EMatchOutcome.kEMatchOutcomeNoTeamWinner
            };
            Write(lobby);
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

            if (lobby.AllMembers.Any(member => member.Id == context.SteamId))
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

            var target = lobby.AllMembers.FirstOrDefault(member => AccountIdOf(member.Id) == accountId);
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

            var target = lobby.AllMembers.FirstOrDefault(member => AccountIdOf(member.Id) == accountId);
            if (target is null || target.Team == DotaGcTeam.DotaGcTeamPlayerPool)
            {
                return;
            }

            target.Team = DotaGcTeam.DotaGcTeamPlayerPool;
            target.Slot = 0;
            Write(lobby);
        }
    }

    /// <summary>
    /// Applies the host's settings. Only the host may, and not once the game is
    /// running. Returns whether the change was applied, for the generic-result
    /// reply the modern client expects.
    /// </summary>
    public bool SetDetails(GcContext context, CMsgPracticeLobbySetDetails details)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby) ||
                lobby.LeaderId != context.SteamId ||
                lobby.state != CSODOTALobby.State.Ui)
            {
                return false;
            }

            ApplyDetails(lobby, details);
            Write(lobby);
            return true;
        }
    }

    /// <summary>
    /// Puts the caller in a team slot. A slot somebody else holds is refused
    /// rather than shared, so two players cannot end up on the same one.
    /// </summary>
    public bool SetTeamSlot(GcContext context, CMsgPracticeLobbySetTeamSlot request)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby) || lobby.state != CSODOTALobby.State.Ui)
            {
                return false;
            }

            var member = lobby.AllMembers.FirstOrDefault(entry => entry.Id == context.SteamId);
            if (member is null)
            {
                return false;
            }

            var team = request.Team;
            if (IsPlayingTeam(team))
            {
                var slot = request.Slot is >= 1 and <= SlotsPerTeam ? request.Slot : FreeSlot(lobby, team);
                if (slot == 0 || lobby.AllMembers.Any(other =>
                        other.Id != member.Id && other.Team == team && other.Slot == slot))
                {
                    return false;
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
            return true;
        }
    }

    /// <summary>
    /// Starts the game. Region 0 (the local listen server the 1v1 flow uses)
    /// moves the lobby to <c>SERVERSETUP</c> with an empty connect string and
    /// lets the game server announce itself; the members draw the launch from
    /// the cache delta and the <see cref="GcMsg.GCGenericResult"/> reply tells
    /// the host's client whether the launch is accepted.
    /// </summary>
    public bool Launch(GcContext context)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby) ||
                lobby.LeaderId != context.SteamId ||
                lobby.state != CSODOTALobby.State.Ui)
            {
                return false;
            }

            // No bots yet: a match needs at least two human players, otherwise
            // the launch would sit in SERVERSETUP forever waiting for a server.
            if (lobby.AllMembers.Count < 2)
            {
                return false;
            }

            MarkTeamsIncomplete(lobby);
            lobby.state = CSODOTALobby.State.Serversetup;
            lobby.Connect = string.Empty;
            lobby.ServerId = 0;
            lobby.GameStartTime = 0;
            lobby.GameState = DOTAGameState.DotaGamerulesStateInit;
            lobby.Lan = lobby.ServerRegion == 0;
            DropServer(lobby.LobbyId);
            Write(lobby);
            return true;
        }
    }

    /// <summary>
    /// The client's cancel button during a launch (GCAbandonCurrentGame):
    /// aborts a launch in progress back to the UI state, or leaves the lobby
    /// when nothing is launching.
    /// </summary>
    public void AbandonCurrentGame(GcContext context)
    {
        lock (_gate)
        {
            if (!TryGetLobbyOf(context.SteamId, out var lobby))
            {
                return;
            }

            if (lobby.state == CSODOTALobby.State.Ui)
            {
                Detach(lobby, context.SteamId);
                return;
            }

            lobby.state = CSODOTALobby.State.Ui;
            lobby.Connect = string.Empty;
            lobby.ServerId = 0;
            lobby.MatchId = 0;
            lobby.GameStartTime = 0;
            lobby.GameState = DOTAGameState.DotaGamerulesStateInit;
            DropServer(lobby.LobbyId);
            Write(lobby);
        }
    }

    /// <summary>
    /// The game server reports where it is. The address is transport state, not
    /// a lobby field: the members only ever see the connect string the GC
    /// builds from it, which is what the clients dial.
    /// </summary>
    public void ServerInfo(GcContext context, CMsgGameServerInfo info)
    {
        lock (_gate)
        {
            var lobby = LobbyForServer(context);
            if (lobby is null)
            {
                return;
            }

            _servers[lobby.LobbyId] = new LobbyServer(
                Ipv4ToString(info.ServerPublicIpAddr),
                Ipv4ToString(info.ServerPrivateIpAddr),
                info.ServerPort != 0 ? info.ServerPort : DefaultServerPort);
            AttachServer(lobby, context.SteamId, running: false);
        }
    }

    /// <summary>
    /// A local listen server announces itself for a lobby. The lobby becomes
    /// reachable (its connect string is published) while still in
    /// <c>SERVERSETUP</c>; the game is only "running" once the server reports
    /// that, see <see cref="ServerAvailable"/>.
    /// </summary>
    public void LanServerAvailable(GcContext context, CMsgLANServerAvailable request)
    {
        lock (_gate)
        {
            if (!TryGetLobby(request.LobbyId, out var lobby))
            {
                return;
            }

            _servers.TryAdd(lobby.LobbyId, new LobbyServer(string.Empty, string.Empty, DefaultServerPort));
            AttachServer(lobby, context.SteamId, running: false);
        }
    }

    /// <summary>
    /// The game server is up and the match is on: the lobby moves to <c>RUN</c>
    /// with a match id and a start time, and every member learns where to
    /// connect from the next cache delta.
    /// </summary>
    public void ServerAvailable(GcContext context)
    {
        lock (_gate)
        {
            var lobby = LobbyForServer(context);
            if (lobby is not null)
            {
                AttachServer(lobby, context.SteamId, running: true);
            }
        }
    }

    /// <summary>
    /// The game server reports who made it in and the live match state. Hero
    /// picks, leaver states and first blood are mirrored onto the lobby object
    /// so reconnecting clients retain that state; the complete 7034 packet is
    /// forwarded by the handler for live kills, lead and building updates.
    /// </summary>
    public CSODOTALobby? ConnectedPlayers(GcContext context, CMsgConnectedPlayers request)
    {
        lock (_gate)
        {
            var lobby = LobbyForServer(context);
            if (lobby is null)
            {
                return null;
            }

            var changed = false;
            if (request.send_reason == CMsgConnectedPlayers.SendReason.GameState ||
                request.GameState != DOTAGameState.DotaGamerulesStateInit)
            {
                if (lobby.GameState != request.GameState)
                {
                    lobby.GameState = request.GameState;
                    changed = true;
                }
            }

            if (request.send_reason is CMsgConnectedPlayers.SendReason.GameState
                or CMsgConnectedPlayers.SendReason.PlayerHero)
            {
                foreach (var player in request.ConnectedPlayers)
                {
                    var member = lobby.AllMembers.FirstOrDefault(entry => entry.Id == player.SteamId);
                    if (member is null)
                    {
                        continue;
                    }

                    if (member.HeroId != (int)player.HeroId)
                    {
                        member.HeroId = (int)player.HeroId;
                        changed = true;
                    }

                    if (member.LeaverStatus != DOTALeaverStatust.DotaLeaverNone)
                    {
                        member.LeaverStatus = DOTALeaverStatust.DotaLeaverNone;
                        changed = true;
                    }
                }

                foreach (var player in request.DisconnectedPlayers)
                {
                    var member = lobby.AllMembers.FirstOrDefault(entry => entry.Id == player.SteamId);
                    if (member is not null && member.LeaverStatus != DOTALeaverStatust.DotaLeaverDisconnected)
                    {
                        member.LeaverStatus = DOTALeaverStatust.DotaLeaverDisconnected;
                        changed = true;
                    }
                }
            }

            if (request.FirstBloodHappened && !lobby.FirstBloodHappened)
            {
                lobby.FirstBloodHappened = true;
                changed = true;
            }

            if (changed)
            {
                Write(lobby);
            }

            return lobby;
        }
    }

    /// <summary>
    /// A player failed to load. The launch aborts: the lobby returns to the UI
    /// state (the client offers to start over) and the failed player is marked
    /// disconnected, exactly what a real GC does.
    /// </summary>
    public void PlayerFailedToConnect(GcContext context, CMsgDOTAPlayerFailedToConnect request)
    {
        lock (_gate)
        {
            var lobby = LobbyForServer(context);
            if (lobby is null)
            {
                return;
            }

            var failedId = request.FailedLoaders.FirstOrDefault();
            if (failedId == 0)
            {
                failedId = request.AbandonedLoaders.FirstOrDefault();
            }

            var member = failedId != 0 ? lobby.AllMembers.FirstOrDefault(entry => entry.Id == failedId) : null;
            if (member is not null)
            {
                member.LeaverStatus = DOTALeaverStatust.DotaLeaverDisconnected;
            }

            lobby.state = CSODOTALobby.State.Ui;
            Write(lobby);
        }
    }

    private void AttachServer(CSODOTALobby lobby, ulong serverSteamId, bool running)
    {
        _servers.TryGetValue(lobby.LobbyId, out var server);
        server ??= new LobbyServer(string.Empty, string.Empty, DefaultServerPort);
        _servers[lobby.LobbyId] = server;
        _serversBySteamId[serverSteamId] = lobby.LobbyId;
        lobby.ServerId = serverSteamId;

        if (running)
        {
            lobby.state = CSODOTALobby.State.Run;
            if (lobby.MatchId == 0)
            {
                lobby.MatchId = lobby.LobbyId;
            }

            if (lobby.GameStartTime == 0)
            {
                lobby.GameStartTime = (uint)_time.GetUtcNow().ToUnixTimeSeconds();
            }

            foreach (var member in lobby.AllMembers)
            {
                member.LeaverStatus = DOTALeaverStatust.DotaLeaverNone;
            }
        }
        else if (lobby.state != CSODOTALobby.State.Run)
        {
            lobby.state = CSODOTALobby.State.Serversetup;
        }

        lobby.Connect = BuildConnectString(server);
        lobby.Lan = lobby.ServerRegion == 0;
        Write(lobby);
    }

    private CSODOTALobby? LobbyOfServer(ulong steamId) =>
        _serversBySteamId.TryGetValue(steamId, out var lobbyId) && TryGetLobby(lobbyId, out var lobby)
            ? lobby
            : null;

    /// <summary>
    /// The lobby a game server talks about. Once attached, the server is mapped
    /// to its lobby; until then (the server's very first message of a launch)
    /// the lobby waiting in <c>SERVERSETUP</c> with no server is the one.
    /// </summary>
    private CSODOTALobby? LobbyForServer(GcContext context) =>
        LobbyOfServer(context.SteamId) ??
        AllLobbies()
            .Where(candidate => candidate.state == CSODOTALobby.State.Serversetup && candidate.ServerId == 0)
            .OrderByDescending(candidate => candidate.LobbyId)
            .FirstOrDefault();

    private void DropServer(ulong lobbyId)
    {
        _servers.Remove(lobbyId);
        foreach (var pair in _serversBySteamId.Where(entry => entry.Value == lobbyId).ToList())
        {
            _serversBySteamId.Remove(pair.Key);
        }
    }

    /// <summary>Both teams start incomplete; the game server completes them.</summary>
    private static void MarkTeamsIncomplete(CSODOTALobby lobby)
    {
        while (lobby.TeamDetails.Count < 2)
        {
            lobby.TeamDetails.Add(new CLobbyTeamDetails());
        }

        for (var i = 0; i < 2; i++)
        {
            lobby.TeamDetails[i].TeamComplete = false;
        }
    }

    private static string BuildConnectString(LobbyServer server)
    {
        var endpoints = new[] { server.PublicIp, server.PrivateIp }
            .Where(ip => ip.Length > 0)
            .Select(ip => $"{ip}:{server.Port}")
            .Distinct()
            .ToList();

        if (endpoints.Count == 0)
        {
            endpoints.Add($"127.0.0.1:{server.Port}");
        }

        return string.Join(" ", endpoints);
    }

    private static string Ipv4ToString(uint value)
    {
        if (value == 0)
        {
            return string.Empty;
        }

        return string.Join(
            ".",
            new[] { value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF, (value >> 24) & 0xFF });
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
        var member = lobby.AllMembers.FirstOrDefault(entry => entry.Id == steamId);
        if (member is null)
        {
            return;
        }

        if (lobby.LeaderId == steamId || lobby.AllMembers.Count <= 1)
        {
            Close(lobby);
            return;
        }

        lobby.AllMembers.Remove(member);
        if (_memberNames.TryGetValue(lobby.LobbyId, out var names))
        {
            names.Remove(steamId);
        }
        _memberships.Remove(steamId);
        _soCache.Unsubscribe(AccountIdOf(steamId), SoOwner.ForLobby(lobby.LobbyId));
        Write(lobby);
    }

    private void Close(CSODOTALobby lobby)
    {
        foreach (var member in lobby.AllMembers)
        {
            _memberships.Remove(member.Id);
        }

        _memberNames.Remove(lobby.LobbyId);
        DropServer(lobby.LobbyId);
        _soCache.RemoveOwner(SoOwner.ForLobby(lobby.LobbyId));
    }

    private void AddMember(CSODOTALobby lobby, GcContext context, DotaGcTeam team)
    {
        lobby.AllMembers.Add(new CSODOTALobbyMember
        {
            Id = context.SteamId,
            Team = team,
            Slot = IsPlayingTeam(team) ? FreeSlot(lobby, team) : 0
        });

        if (!_memberNames.TryGetValue(lobby.LobbyId, out var names))
        {
            names = _memberNames[lobby.LobbyId] = [];
        }

        names[context.SteamId] = context.PersonaName;
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
        var taken = lobby.AllMembers
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

    private CMsgPracticeLobbyListResponseEntry ToListEntry(CSODOTALobby lobby)
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

        foreach (var member in lobby.AllMembers)
        {
            entry.Members.Add(new CMsgPracticeLobbyListResponseEntry.CLobbyMember
            {
                AccountId = AccountIdOf(member.Id),
                PlayerName = MemberName(lobby.LobbyId, member.Id)
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

    private void Write(CSODOTALobby lobby)
    {
        // The modern client reads member_indices to know who is in the room:
        // without them the lobby renders empty even though all_members is set.
        lobby.MemberIndices = lobby.AllMembers.Count == 0
            ? []
            : Enumerable.Range(0, lobby.AllMembers.Count).Select(index => (uint)index).ToArray();

        if (lobby.LobbyCreationTime == 0)
        {
            // The lobby id embeds the creation second in its high bits.
            lobby.LobbyCreationTime = (uint)(lobby.LobbyId >> SequenceBits);
        }

        if (lobby.ExtraMessages.Count == 0)
        {
            lobby.ExtraMessages.Add(new CSODOTALobby.CExtraMsg
            {
                Id = 8821,
                Contents = new byte[] { 8, 0 }
            });
        }

        _soCache.Set(
            SoCacheKey.Lobby(lobby.LobbyId),
            new SoObjectKey(DotaSoCache.TypeDotaLobby, lobby.LobbyId),
            lobby);
        WriteAuxiliary(lobby);
    }

    /// <summary>
    /// The other buckets of the lobby cache the modern client subscribes to:
    /// the empty invite bucket, the static lobby (names), the server lobby and
    /// the server static lobby. Mirrors what a real GC publishes.
    /// </summary>
    private void WriteAuxiliary(CSODOTALobby lobby)
    {
        var key = SoCacheKey.Lobby(lobby.LobbyId);
        _soCache.Set(
            key,
            new SoObjectKey(DotaSoCache.TypeDotaLobbyInviteBucket, lobby.LobbyId),
            new CSODOTALobbyInvite());

        var staticLobby = new CSODOTAStaticLobby { IsPlayerDraft = false };
        var serverLobby = new CSODOTAServerLobby();
        var serverStatic = new CSODOTAServerStaticLobby();

        foreach (var member in lobby.AllMembers)
        {
            var name = MemberName(lobby.LobbyId, member.Id);
            var rank = _ranks.GetOrCreate(AccountIdOf(member.Id));
            staticLobby.AllMembers.Add(new CSODOTAStaticLobbyMember { Name = name });
            serverLobby.AllMembers.Add(new CSODOTAServerLobbyMember());
            serverStatic.AllMembers.Add(new CSODOTAServerStaticLobbyMember
            {
                SteamId = member.Id,
                RankTier = RankMath.VisibleRankFor(rank).RankValue,
                WasMvpLastGame = false,
                IsPlusSubscriber = true,
                FavoriteTeamPacked = 0,
                IsSteamChina = false,
                BannedHeroIds = new[] { 75, 0, 0, 0 }
            });
        }

        _soCache.Set(key, new SoObjectKey(DotaSoCache.TypeDotaStaticLobby, lobby.LobbyId), staticLobby);
        _soCache.Set(key, new SoObjectKey(DotaSoCache.TypeDotaServerLobby, lobby.LobbyId), serverLobby);
        _soCache.Set(key, new SoObjectKey(DotaSoCache.TypeDotaServerStaticLobby, lobby.LobbyId), serverStatic);
    }

    /// <summary>The persona name a member joined with (the lobby SO carries no names).</summary>
    public string MemberName(ulong lobbyId, ulong steamId) =>
        _memberNames.TryGetValue(lobbyId, out var names) && names.TryGetValue(steamId, out var name)
            ? name
            : string.Empty;

    private static uint AccountIdOf(ulong steamId) => SteamAccount.AccountIdFromSteamId(steamId);

    /// <summary>Lobby ids in the same shape as party ids: the current second in the high bits, a counter in the low ones.</summary>
    private ulong NextId()
    {
        _sequence = (_sequence + 1) & SequenceMask;
        return ((ulong)_time.GetUtcNow().ToUnixTimeSeconds() << SequenceBits) | _sequence;
    }

    /// <summary>Transport state of a launched game server, kept out of the SO.</summary>
    private sealed record LobbyServer(string PublicIp, string PrivateIp, uint Port);
}
