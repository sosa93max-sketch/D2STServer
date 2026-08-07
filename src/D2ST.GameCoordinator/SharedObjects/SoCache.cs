using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.SharedObjects;

/// <summary>
/// The objects of one Shared Object cache plus the accounts subscribed to it.
/// <para>
/// The client mirrors this dictionary and refuses to act on an object it never
/// saw created, so every mutation has to be published to the subscribers as a
/// create/update/destroy — that is what <see cref="SoCacheService"/> does on top
/// of this type. The version is bumped on every mutation because the client
/// compares it with the one it holds to decide whether it is stale.
/// </para>
/// Instances are guarded by the store's lock; the type itself is not thread safe.
/// </summary>
public sealed class SoCache
{
    private readonly Dictionary<SoObjectKey, byte[]> _objects = [];
    private readonly HashSet<uint> _subscribers = [];
    private readonly HashSet<int> _emptyTypes = [];

    public SoCache(SoCacheKey key, ulong version)
    {
        Key = key;
        Version = version;
    }

    public SoCacheKey Key { get; }

    public ulong Version { get; private set; }

    public IReadOnlyCollection<uint> Subscribers => _subscribers;

    public bool Contains(SoObjectKey objectKey) => _objects.ContainsKey(objectKey);

    public bool AddSubscriber(uint accountId) => _subscribers.Add(accountId);

    public bool RemoveSubscriber(uint accountId) => _subscribers.Remove(accountId);

    /// <summary>Writes an object and returns whether it did not exist yet.</summary>
    public bool Set(SoObjectKey objectKey, byte[] objectData)
    {
        var created = _objects.TryAdd(objectKey, objectData);
        if (!created)
        {
            _objects[objectKey] = objectData;
        }

        Version++;
        return created;
    }

    public bool TryRemove(SoObjectKey objectKey, out byte[] objectData)
    {
        if (!_objects.Remove(objectKey, out var removed))
        {
            objectData = [];
            return false;
        }

        Version++;
        objectData = removed;
        return true;
    }

    /// <summary>Every object of one type, with its key.</summary>
    public IReadOnlyList<KeyValuePair<SoObjectKey, byte[]>> OfType(int typeId) =>
        _objects.Where(entry => entry.Key.TypeId == typeId).ToList();

    public bool TryGet(SoObjectKey objectKey, out byte[] objectData)
    {
        if (_objects.TryGetValue(objectKey, out var found))
        {
            objectData = found;
            return true;
        }

        objectData = [];
        return false;
    }

    /// <summary>
    /// The whole cache as the subscription message. <paramref name="peerServiceIds"/>
    /// is the <c>service_list</c> field: the other services publishing a cache for
    /// the same owner, which the client uses to know how many subscriptions to wait
    /// for before it considers itself synchronized.
    /// </summary>
    public CMsgSOCacheSubscribed ToSubscribed(IEnumerable<uint> peerServiceIds)
    {
        var subscribed = new CMsgSOCacheSubscribed
        {
            Version = Version,
            SyncVersion = 1,
            ServiceId = Key.ServiceId,
            ServiceLists = peerServiceIds.ToArray(),
            OwnerSoid = Key.Owner.ToProto()
        };

        foreach (var group in _objects.GroupBy(entry => entry.Key.TypeId).OrderBy(group => group.Key))
        {
            var type = new CMsgSOCacheSubscribed.SubscribedType { TypeId = group.Key };
            foreach (var entry in group)
            {
                type.ObjectDatas.Add(entry.Value);
            }

            subscribed.Objects.Add(type);
        }

        foreach (var typeId in _emptyTypes.Where(typeId => _objects.Keys.All(key => key.TypeId != typeId)).Order())
        {
            subscribed.Objects.Add(new CMsgSOCacheSubscribed.SubscribedType { TypeId = typeId });
        }

        return subscribed;
    }

    /// <summary>
    /// Declares a type the client must know about even while the owner has no
    /// object of it (an empty inventory still needs its bucket). The type is only
    /// emitted in the subscription message, never as a create.
    /// </summary>
    public void DeclareEmptyType(int typeId) => _emptyTypes.Add(typeId);
}
