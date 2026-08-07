using D2ST.Core.Accounts;
using D2ST.Core.Events;
using D2ST.Core.Lobbies;
using D2ST.Core.Steam;
using D2ST.Steam.Events;
using D2ST.Steam.Presence;

namespace D2ST.Steam.Lobbies;

/// <summary>
/// In-memory lobby directory. Lobbies only exist while someone is in them, so
/// there is nothing to persist: a restart legitimately means every lobby is
/// gone, exactly as it would be if Steam's matchmaking service restarted.
/// </summary>
public sealed class LobbyService : ILobbyService
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, LobbyState> _lobbies = new();
    private readonly IEventStream _events;
    private readonly IPresenceTracker _presence;
    private uint _sequence;

    public LobbyService(IEventStream events, IPresenceTracker presence)
    {
        _events = events;
        _presence = presence;
    }

    public Lobby Create(
        SteamSession session,
        uint appId,
        int lobbyType,
        int maxMembers,
        IReadOnlyDictionary<string, string>? lobbyData)
    {
        Lobby snapshot;
        lock (_sync)
        {
            var state = new LobbyState(LobbyIds.FromSequence(++_sequence), appId)
            {
                OwnerSteamId = session.Account.SteamId,
                LobbyType = lobbyType,
                // Steam clamps to at least the creator; a lobby of 0 members
                // would be unjoinable by the player who just made it.
                MaxMembers = maxMembers > 0 ? maxMembers : 1
            };

            foreach (var (key, value) in lobbyData ?? new Dictionary<string, string>())
            {
                state.Data[key] = value;
            }

            state.Members[session.Account.AccountId] = new MemberState(session.Account.SteamId);
            _lobbies[state.SteamId] = state;
            snapshot = state.ToSnapshot();
        }

        _presence.SetLobby(session.Account.AccountId, snapshot.SteamId);
        Publish(snapshot, SteamEventTypes.LobbyCreated, session.Account.SteamId);
        return snapshot;
    }

    public Lobby? Find(ulong lobbyId)
    {
        lock (_sync)
        {
            return _lobbies.TryGetValue(lobbyId, out var state) ? state.ToSnapshot() : null;
        }
    }

    public IReadOnlyList<Lobby> Query(LobbyQuery query)
    {
        List<Lobby> matches;
        lock (_sync)
        {
            matches = _lobbies.Values
                .Where(state => state.Matches(query))
                .Select(state => state.ToSnapshot())
                .ToList();
        }

        foreach (var near in query.NearValueFilters.Reverse())
        {
            matches = matches
                .OrderBy(lobby => Math.Abs(ReadNumeric(lobby, near.Key) - near.Value))
                .ToList();
        }

        return query.ResultCount > 0 && matches.Count > query.ResultCount
            ? matches.GetRange(0, query.ResultCount)
            : matches;
    }

    public Lobby? Join(SteamSession session, ulong lobbyId)
    {
        Lobby snapshot;
        lock (_sync)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var state))
            {
                return null;
            }

            var accountId = session.Account.AccountId;
            // Rejoining is not an error: the client re-issues a join after a
            // reconnect and expects the lobby back, not a "full" failure.
            if (!state.Members.ContainsKey(accountId))
            {
                if (!state.Joinable || state.Members.Count >= state.MaxMembers)
                {
                    return null;
                }

                state.Members[accountId] = new MemberState(session.Account.SteamId);
            }

            snapshot = state.ToSnapshot();
        }

        _presence.SetLobby(session.Account.AccountId, lobbyId);
        Publish(snapshot, SteamEventTypes.LobbyJoined, session.Account.SteamId);
        return snapshot;
    }

    public bool Leave(SteamSession session, ulong lobbyId) => Leave(session.Account.AccountId, session.Account.SteamId, lobbyId);

    public void LeaveAll(uint accountId)
    {
        ulong[] lobbyIds;
        lock (_sync)
        {
            lobbyIds = _lobbies
                .Where(entry => entry.Value.Members.ContainsKey(accountId))
                .Select(entry => entry.Key)
                .ToArray();
        }

        foreach (var lobbyId in lobbyIds)
        {
            Leave(accountId, SteamAccount.SteamIdFromAccountId(accountId), lobbyId);
        }
    }

    public bool SetLobbyData(SteamSession session, ulong lobbyId, string key, string? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return Mutate(session, lobbyId, ownerOnly: true, state =>
        {
            if (string.IsNullOrEmpty(value))
            {
                state.Data.Remove(key);
            }
            else
            {
                state.Data[key] = value;
            }
        }, SteamEventTypes.LobbyUpdated);
    }

    public bool SetMemberData(SteamSession session, ulong lobbyId, string key, string? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return Mutate(session, lobbyId, ownerOnly: false, state =>
        {
            var member = state.Members[session.Account.AccountId];
            if (string.IsNullOrEmpty(value))
            {
                member.Data.Remove(key);
            }
            else
            {
                member.Data[key] = value;
            }
        }, SteamEventTypes.LobbyMemberUpdated);
    }

    public bool SetGameServer(SteamSession session, ulong lobbyId, ulong gameServerSteamId, uint ip, uint port) =>
        Mutate(
            session,
            lobbyId,
            ownerOnly: true,
            state => state.GameServer = new LobbyGameServer(gameServerSteamId, ip, port),
            // The client follows the lobby to the server on this event, so it
            // is only raised once the address is actually usable.
            ip != 0 && port != 0 ? SteamEventTypes.LobbyGameCreated : SteamEventTypes.LobbyUpdated);

    public bool UpdateSettings(SteamSession session, ulong lobbyId, LobbySettingsUpdate update) =>
        Mutate(session, lobbyId, ownerOnly: true, state =>
        {
            state.Joinable = update.Joinable ?? state.Joinable;
            state.LobbyType = update.LobbyType ?? state.LobbyType;
            state.MaxMembers = update.MaxMembers is > 0 ? update.MaxMembers.Value : state.MaxMembers;

            // Ownership can only be handed to someone already in the lobby.
            if (update.OwnerSteamId is { } owner &&
                state.Members.Values.Any(member => member.SteamId == owner))
            {
                state.OwnerSteamId = owner;
            }
        }, SteamEventTypes.LobbyUpdated);

    public bool SendChat(SteamSession session, ulong lobbyId, string messageBase64)
    {
        Lobby snapshot;
        lock (_sync)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var state) ||
                !state.Members.ContainsKey(session.Account.AccountId))
            {
                return false;
            }

            snapshot = state.ToSnapshot();
        }

        Publish(snapshot, SteamEventTypes.LobbyChat, session.Account.SteamId, steamEvent => steamEvent with
        {
            PayloadBase64 = messageBase64
        });

        return true;
    }

    public bool Invite(SteamSession session, ulong lobbyId, ulong inviteeSteamId)
    {
        Lobby snapshot;
        lock (_sync)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var state) ||
                !state.Members.ContainsKey(session.Account.AccountId))
            {
                return false;
            }

            snapshot = state.ToSnapshot();
        }

        _events.Publish(SteamAccount.AccountIdFromSteamId(inviteeSteamId), new SteamEvent
        {
            Type = SteamEventTypes.LobbyInvite,
            SteamId = session.Account.SteamId,
            AccountId = session.Account.AccountId,
            PersonaName = session.PersonaName ?? string.Empty,
            AppId = snapshot.AppId,
            LobbyId = snapshot.SteamId
        });

        return true;
    }

    private bool Leave(uint accountId, ulong steamId, ulong lobbyId)
    {
        Lobby snapshot;
        bool removed;
        lock (_sync)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var state) || !state.Members.Remove(accountId))
            {
                return false;
            }

            removed = state.Members.Count == 0;
            if (removed)
            {
                _lobbies.Remove(lobbyId);
            }
            else if (state.OwnerSteamId == steamId)
            {
                // Steam promotes another member instead of orphaning the lobby.
                state.OwnerSteamId = state.Members.Values.First().SteamId;
            }

            snapshot = state.ToSnapshot();
        }

        _presence.SetLobby(accountId, 0);

        // The leaver is told too: it is how its own client confirms the exit.
        _events.Publish(accountId, LobbyEvent(snapshot, SteamEventTypes.LobbyLeft, steamId));
        if (!removed)
        {
            Publish(snapshot, SteamEventTypes.LobbyLeft, steamId);
        }

        return true;
    }

    private bool Mutate(
        SteamSession session,
        ulong lobbyId,
        bool ownerOnly,
        Action<LobbyState> mutate,
        string eventType)
    {
        Lobby snapshot;
        lock (_sync)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var state) ||
                !state.Members.ContainsKey(session.Account.AccountId) ||
                (ownerOnly && state.OwnerSteamId != session.Account.SteamId))
            {
                return false;
            }

            mutate(state);
            snapshot = state.ToSnapshot();
        }

        Publish(snapshot, eventType, session.Account.SteamId);
        return true;
    }

    private void Publish(
        Lobby lobby,
        string type,
        ulong subjectSteamId,
        Func<SteamEvent, SteamEvent>? decorate = null)
    {
        var steamEvent = LobbyEvent(lobby, type, subjectSteamId);
        if (decorate is not null)
        {
            steamEvent = decorate(steamEvent);
        }

        foreach (var member in lobby.Members)
        {
            _events.Publish(member.AccountId, steamEvent);
        }
    }

    private static SteamEvent LobbyEvent(Lobby lobby, string type, ulong subjectSteamId) => new()
    {
        Type = type,
        SteamId = subjectSteamId,
        AccountId = SteamAccount.AccountIdFromSteamId(subjectSteamId),
        AppId = lobby.AppId,
        LobbyId = lobby.SteamId,
        GameServerSteamId = lobby.GameServer.SteamId,
        GameServerIp = lobby.GameServer.Ip,
        GameServerPort = (ushort)lobby.GameServer.Port,
        Lobby = lobby
    };

    private static int ReadNumeric(Lobby lobby, string key) =>
        lobby.LobbyData.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : 0;

    private sealed class LobbyState(ulong steamId, uint appId)
    {
        public ulong SteamId { get; } = steamId;

        public uint AppId { get; } = appId;

        public ulong OwnerSteamId { get; set; }

        public int LobbyType { get; set; }

        public int MaxMembers { get; set; } = 1;

        public bool Joinable { get; set; } = true;

        public LobbyGameServer GameServer { get; set; } = LobbyGameServer.None;

        public Dictionary<string, string> Data { get; } = new(StringComparer.Ordinal);

        public Dictionary<uint, MemberState> Members { get; } = new();

        public Lobby ToSnapshot() => new()
        {
            SteamId = SteamId,
            AppId = AppId,
            OwnerSteamId = OwnerSteamId,
            LobbyType = LobbyType,
            MaxMembers = MaxMembers,
            Joinable = Joinable,
            LobbyData = new Dictionary<string, string>(Data, StringComparer.Ordinal),
            Members = Members
                .Select(entry => new LobbyMember(
                    entry.Value.SteamId,
                    entry.Key,
                    new Dictionary<string, string>(entry.Value.Data, StringComparer.Ordinal)))
                .ToList(),
            GameServer = GameServer
        };

        public bool Matches(LobbyQuery query)
        {
            // Only public lobbies are discoverable; the others are reached
            // through an invite or a friend's presence.
            if (Joinable is false || LobbyType != PublicLobbyType || (query.AppId != 0 && query.AppId != AppId))
            {
                return false;
            }

            if (query.SlotsAvailable > 0 && MaxMembers - Members.Count < query.SlotsAvailable)
            {
                return false;
            }

            return query.StringFilters.All(MatchesString) && query.NumericalFilters.All(MatchesNumerical);
        }

        private const int PublicLobbyType = 2;

        private bool MatchesString(LobbyStringFilter filter)
        {
            var value = Data.TryGetValue(filter.Key, out var raw) ? raw : string.Empty;
            var comparison = string.CompareOrdinal(value, filter.Value);
            return filter.Comparison == LobbyComparison.NotEqual ? comparison != 0 : comparison == 0;
        }

        private bool MatchesNumerical(LobbyNumericalFilter filter)
        {
            var value = Data.TryGetValue(filter.Key, out var raw) && int.TryParse(raw, out var parsed) ? parsed : 0;
            return filter.Comparison switch
            {
                LobbyComparison.EqualToOrLessThan => value <= filter.Value,
                LobbyComparison.LessThan => value < filter.Value,
                LobbyComparison.GreaterThan => value > filter.Value,
                LobbyComparison.EqualToOrGreaterThan => value >= filter.Value,
                LobbyComparison.NotEqual => value != filter.Value,
                _ => value == filter.Value
            };
        }
    }

    private sealed class MemberState(ulong steamId)
    {
        public ulong SteamId { get; } = steamId;

        public Dictionary<string, string> Data { get; } = new(StringComparer.Ordinal);
    }
}
