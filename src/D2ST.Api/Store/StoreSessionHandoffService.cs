using System.Collections.Concurrent;
using System.Security.Cryptography;
using D2ST.Core.Steam;
using D2ST.Steam;

namespace D2ST.Api.Store;

public sealed record StoreSessionHandoff(string Code, DateTimeOffset ExpiresAt);

/// <summary>
/// Short-lived, single-use bridge from the launcher's authenticated bearer
/// session to the same-origin browser store. Only the opaque code is exposed to
/// the browser; the bearer token remains inside the server session table.
/// </summary>
public sealed class StoreSessionHandoffService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(90);
    private const int MaxEntries = 2048;

    private readonly ConcurrentDictionary<string, PendingHandoff> _pending = new(StringComparer.Ordinal);
    private readonly ISessionStore _sessions;
    private readonly TimeProvider _time;

    public StoreSessionHandoffService(ISessionStore sessions, TimeProvider time)
    {
        _sessions = sessions;
        _time = time;
    }

    public StoreSessionHandoff Create(SteamSession session)
    {
        Prune();

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = _time.GetUtcNow().Add(Lifetime);
        _pending[code] = new PendingHandoff(session.Token, session.Account.AccountId, expiresAt);
        return new StoreSessionHandoff(code, expiresAt);
    }

    public SteamSession? Consume(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || !_pending.TryRemove(code.Trim(), out var handoff))
        {
            return null;
        }

        if (handoff.ExpiresAt <= _time.GetUtcNow())
        {
            return null;
        }

        var session = _sessions.Find(handoff.SessionToken);
        return session is not null && session.Account.AccountId == handoff.AccountId
            ? session
            : null;
    }

    private void Prune()
    {
        var now = _time.GetUtcNow();
        foreach (var pair in _pending)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _pending.TryRemove(pair.Key, out _);
            }
        }

        if (_pending.Count <= MaxEntries)
        {
            return;
        }

        foreach (var pair in _pending.OrderBy(pair => pair.Value.ExpiresAt).Take(_pending.Count - MaxEntries))
        {
            _pending.TryRemove(pair.Key, out _);
        }
    }

    private sealed record PendingHandoff(
        string SessionToken,
        uint AccountId,
        DateTimeOffset ExpiresAt);
}
