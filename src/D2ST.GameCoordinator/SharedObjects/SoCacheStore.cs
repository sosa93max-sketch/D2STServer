namespace D2ST.GameCoordinator.SharedObjects;

/// <summary>
/// Every live Shared Object cache, keyed by owner and service. Caches are
/// volatile on purpose: they are the GC's view of the current session (account,
/// econ, party, lobby), rebuilt from persistence at logon rather than stored.
/// <para>
/// All access runs under one lock. Mutations are short dictionary writes and the
/// alternative — a lock per cache — would still need a global one to create and
/// destroy caches, which is exactly what parties and lobbies do constantly.
/// </para>
/// </summary>
public sealed class SoCacheStore
{
    private readonly Dictionary<SoCacheKey, SoCache> _caches = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// Runs <paramref name="action"/> against the cache, creating it if needed,
    /// while holding the store lock. The cache must not escape the callback.
    /// </summary>
    public T Mutate<T>(SoCacheKey key, Func<SoCache, T> action)
    {
        lock (_gate)
        {
            if (!_caches.TryGetValue(key, out var cache))
            {
                cache = new SoCache(key, version: 1);
                _caches.Add(key, cache);
            }

            return action(cache);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> against an existing cache and returns
    /// whether it existed. Nothing is created: a caller asking about a party that
    /// is already gone must not resurrect it.
    /// </summary>
    public bool TryRead<T>(SoCacheKey key, Func<SoCache, T> action, out T result)
    {
        lock (_gate)
        {
            if (!_caches.TryGetValue(key, out var cache))
            {
                result = default!;
                return false;
            }

            result = action(cache);
            return true;
        }
    }

    /// <summary>All caches of one owner, in service order (game first, econ next).</summary>
    public IReadOnlyList<uint> ServicesOf(SoOwner owner)
    {
        lock (_gate)
        {
            return _caches.Keys
                .Where(key => key.Owner == owner)
                .Select(key => key.ServiceId)
                .Order()
                .ToList();
        }
    }

    /// <summary>Drops a cache and returns the accounts that were subscribed to it.</summary>
    public IReadOnlyList<uint> Remove(SoCacheKey key)
    {
        lock (_gate)
        {
            if (!_caches.Remove(key, out var cache))
            {
                return [];
            }

            return cache.Subscribers.ToList();
        }
    }
}
