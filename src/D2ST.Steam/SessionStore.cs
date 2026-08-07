using System.Collections.Concurrent;
using D2ST.Core.Steam;
using Microsoft.Extensions.Options;

namespace D2ST.Steam;

/// <summary>
/// In-memory session table. Sessions are process-lifetime only: a server restart
/// forces the shim to re-run its logon handshake, which it already does whenever
/// a call comes back 401.
/// </summary>
public sealed class SessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SteamSession> _sessions = new(StringComparer.Ordinal);
    private readonly IOptions<SteamOptions> _options;
    private readonly TimeProvider _time;

    public SessionStore(IOptions<SteamOptions> options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    public void Add(SteamSession session)
    {
        session.LastSeenAt = _time.GetUtcNow();
        _sessions[session.Token] = session;
    }

    public SteamSession? Find(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !_sessions.TryGetValue(token, out var session))
        {
            return null;
        }

        if (_time.GetUtcNow() - session.LastSeenAt > _options.Value.SessionTimeout)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        return session;
    }

    public bool Remove(string? token) =>
        !string.IsNullOrWhiteSpace(token) && _sessions.TryRemove(token, out _);

    public void Touch(SteamSession session) => session.LastSeenAt = _time.GetUtcNow();

    public bool IsOnline(uint accountId)
    {
        var since = _time.GetUtcNow() - _options.Value.PresenceTimeout;
        return _sessions.Values.Any(session => IsLiveClient(session, accountId, since));
    }

    public IReadOnlyCollection<uint> OnlineAccounts()
    {
        var since = _time.GetUtcNow() - _options.Value.PresenceTimeout;
        return _sessions.Values
            .Where(session => session.ProcessRole == ProcessRoles.Client && session.LastSeenAt >= since)
            .Select(session => session.Account.AccountId)
            .ToHashSet();
    }

    public int RemoveClientSessions(uint accountId)
    {
        var removed = 0;
        foreach (var pair in _sessions)
        {
            if (pair.Value.Account.AccountId == accountId &&
                pair.Value.ProcessRole == ProcessRoles.Client &&
                _sessions.TryRemove(pair))
            {
                removed++;
            }
        }

        return removed;
    }

    public int RemoveAll(uint accountId)
    {
        var removed = 0;
        foreach (var pair in _sessions)
        {
            if (pair.Value.Account.AccountId == accountId && _sessions.TryRemove(pair))
            {
                removed++;
            }
        }

        return removed;
    }

    private static bool IsLiveClient(SteamSession session, uint accountId, DateTimeOffset since) =>
        session.Account.AccountId == accountId &&
        session.ProcessRole == ProcessRoles.Client &&
        session.LastSeenAt >= since;
}
