using D2ST.Core.GameCoordinator;
using D2ST.GameCoordinator.Messaging;
using D2ST.Protocol;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.SharedObjects;

/// <summary>
/// Writes to the Shared Object caches and tells the subscribers about it.
/// <para>
/// This is the half of stage 4 that party, lobby and econ need: the client keeps
/// its own copy of every cache it is subscribed to and only ever reacts to
/// deltas, so a write that is not published as create/update/destroy is a write
/// the client will never see.
/// </para>
/// </summary>
public sealed class SoCacheService
{
    private readonly SoCacheStore _store;
    private readonly IGcMessageQueue _queue;
    private readonly IGcProtoCodec _codec;

    public SoCacheService(SoCacheStore store, IGcMessageQueue queue, IGcProtoCodec codec)
    {
        _store = store;
        _queue = queue;
        _codec = codec;
    }

    /// <summary>Declares a type the subscription message must list even while empty.</summary>
    public void DeclareEmptyType(SoCacheKey key, int typeId) =>
        _store.Mutate(key, cache =>
        {
            cache.DeclareEmptyType(typeId);
            return true;
        });

    /// <summary>
    /// Writes an object and publishes it as a create (first write) or an update.
    /// </summary>
    public void Set<T>(SoCacheKey key, SoObjectKey objectKey, T value)
    {
        var body = _codec.Encode(value);
        var (created, version, subscribers) = _store.Mutate(key, cache =>
        {
            var isNew = cache.Set(objectKey, body);
            return (isNew, cache.Version, cache.Subscribers.ToArray());
        });

        Publish(
            subscribers,
            created ? GcMsg.SoCreate : GcMsg.SoUpdate,
            SingleObject(key, objectKey.TypeId, body, version));
    }

    /// <summary>
    /// Writes an object only if the cache does not have it yet, without
    /// publishing anything. This is how a cache is seeded before anyone is
    /// subscribed to it (logon): re-seeding on a reconnect must not look like an
    /// update to a client whose copy is already correct.
    /// </summary>
    public void SeedIfAbsent<T>(SoCacheKey key, SoObjectKey objectKey, Func<T> factory) =>
        _store.Mutate(key, cache => cache.Contains(objectKey) || cache.Set(objectKey, _codec.Encode(factory())));

    /// <summary>Removes an object and publishes the destroy, if it was there.</summary>
    public bool Destroy(SoCacheKey key, SoObjectKey objectKey)
    {
        var read = _store.TryRead(
            key,
            cache =>
            {
                var found = cache.TryRemove(objectKey, out var body);
                return new DestroyResult(found, body, cache.Version, cache.Subscribers.ToArray());
            },
            out var result);

        if (!read || !result.Found)
        {
            return false;
        }

        Publish(
            result.Subscribers,
            GcMsg.SoDestroy,
            SingleObject(key, objectKey.TypeId, result.Body, result.Version));
        return true;
    }

    /// <summary>
    /// Subscribes an account to every cache of an owner and returns the
    /// subscription messages, in service order. The caller decides how they
    /// travel: the welcome embeds them, everything else pushes them.
    /// </summary>
    public IReadOnlyList<CMsgSOCacheSubscribed> Subscribe(uint accountId, SoOwner owner)
    {
        var services = _store.ServicesOf(owner);
        var subscribed = new List<CMsgSOCacheSubscribed>(services.Count);

        foreach (var serviceId in services)
        {
            var key = new SoCacheKey(owner, serviceId);
            var peers = services.Where(other => other != serviceId).ToArray();
            if (_store.TryRead(key, cache =>
                {
                    cache.AddSubscriber(accountId);
                    return cache.ToSubscribed(peers);
                }, out var message))
            {
                subscribed.Add(message);
            }
        }

        return subscribed;
    }

    /// <summary>Subscribes an account and pushes the subscription messages to it.</summary>
    public void PushSubscribe(uint accountId, SoOwner owner)
    {
        foreach (var subscribed in Subscribe(accountId, owner))
        {
            _queue.Enqueue(accountId, new GcMessage(GcMsg.SoCacheSubscribed, _codec.Encode(subscribed)));
        }
    }

    /// <summary>
    /// Stops an account from receiving a cache and tells it to drop its copy.
    /// </summary>
    public void Unsubscribe(uint accountId, SoOwner owner)
    {
        foreach (var serviceId in _store.ServicesOf(owner))
        {
            _store.TryRead(new SoCacheKey(owner, serviceId), cache => cache.RemoveSubscriber(accountId), out _);
        }

        _queue.Enqueue(
            accountId,
            new GcMessage(
                GcMsg.SoCacheUnsubscribed,
                _codec.Encode(new CMsgSOCacheUnsubscribed { OwnerSoid = owner.ToProto() })));
    }

    /// <summary>
    /// Destroys every cache of an owner (a party that disbanded, a lobby that
    /// closed) and unsubscribes everyone who was watching it.
    /// </summary>
    public void RemoveOwner(SoOwner owner)
    {
        var unsubscribed = new CMsgSOCacheUnsubscribed { OwnerSoid = owner.ToProto() };
        var body = _codec.Encode(unsubscribed);

        foreach (var serviceId in _store.ServicesOf(owner))
        {
            foreach (var accountId in _store.Remove(new SoCacheKey(owner, serviceId)))
            {
                _queue.Enqueue(accountId, new GcMessage(GcMsg.SoCacheUnsubscribed, body));
            }
        }
    }

    /// <summary>Reads one object back, decoded. False when the cache or the object is gone.</summary>
    public bool TryGetObject<T>(SoCacheKey key, SoObjectKey objectKey, out T value)
    {
        var found = _store.TryRead(
            key,
            cache => cache.TryGet(objectKey, out var body) ? body : null,
            out var body);

        if (!found || body is null)
        {
            value = default!;
            return false;
        }

        value = _codec.Decode<T>(body);
        return true;
    }

    /// <summary>Every object of one type in a cache, decoded, keyed as stored.</summary>
    public IReadOnlyList<KeyValuePair<SoObjectKey, T>> ObjectsOfType<T>(SoCacheKey key, int typeId) =>
        _store.TryRead(key, cache => cache.OfType(typeId), out var objects)
            ? objects.Select(entry => KeyValuePair.Create(entry.Key, _codec.Decode<T>(entry.Value))).ToList()
            : [];

    /// <summary>The cache's current version, or 0 when it does not exist.</summary>
    public ulong VersionOf(SoCacheKey key) => _store.TryRead(key, cache => cache.Version, out var version) ? version : 0;

    /// <summary>Whether an account currently receives any cache of an owner.</summary>
    public bool IsSubscriber(SoOwner owner, uint accountId) =>
        _store.ServicesOf(owner).Any(serviceId =>
            _store.TryRead(new SoCacheKey(owner, serviceId), cache => cache.Subscribers.Contains(accountId), out var subscribed)
            && subscribed);

    /// <summary>The current contents of one cache, or null if it does not exist.</summary>
    public CMsgSOCacheSubscribed? Snapshot(SoCacheKey key)
    {
        var peers = _store.ServicesOf(key.Owner).Where(service => service != key.ServiceId).ToArray();
        return _store.TryRead(key, cache => cache.ToSubscribed(peers), out var subscribed) ? subscribed : null;
    }

    /// <summary>Every cache of an owner as subscription messages, in service order.</summary>
    public IReadOnlyList<CMsgSOCacheSubscribed> SnapshotOwner(SoOwner owner) =>
        _store.ServicesOf(owner)
            .Select(serviceId => Snapshot(new SoCacheKey(owner, serviceId)))
            .OfType<CMsgSOCacheSubscribed>()
            .ToList();

    private void Publish(IReadOnlyCollection<uint> subscribers, uint messageType, CMsgSOSingleObject payload)
    {
        if (subscribers.Count == 0)
        {
            return;
        }

        var body = _codec.Encode(payload);
        foreach (var accountId in subscribers)
        {
            _queue.Enqueue(accountId, new GcMessage(messageType, body));
        }
    }

    private sealed record DestroyResult(bool Found, byte[] Body, ulong Version, IReadOnlyCollection<uint> Subscribers);

    private static CMsgSOSingleObject SingleObject(SoCacheKey key, int typeId, byte[] body, ulong version) =>
        new()
        {
            TypeId = typeId,
            ObjectData = body,
            Version = version,
            ServiceId = key.ServiceId,
            OwnerSoid = key.Owner.ToProto()
        };
}
