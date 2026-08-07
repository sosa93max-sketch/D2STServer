using D2ST.GameCoordinator.SharedObjects;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Econ;

/// <summary>
/// The player's items, which live in the econ Shared Object cache rather than
/// in a table of their own: the client already mirrors that cache, so writing
/// an item through <see cref="SoCacheService"/> both stores it and publishes the
/// delta that makes the armory redraw.
/// <para>
/// There is no item catalogue, so an account starts empty and every request for
/// an item it does not hold answers "you do not own that". Items enter through
/// <see cref="Grant"/>, which is what the econ endpoint calls, and the ownership
/// rules below apply the same way once a catalogue feeds that grant.
/// </para>
/// </summary>
public sealed class EconInventory
{
    /// <summary>Styles are one byte; 255 is the client's "no style" sentinel.</summary>
    private const uint StyleNone = 0;
    private const uint StyleDefaultSentinel = 255;

    /// <summary>A granted item takes the shape Dota expects of a plain owned cosmetic.</summary>
    private const uint DefaultQuality = 4;
    private const uint DefaultOrigin = 2;
    private const uint DefaultInventoryPosition = 1;

    private readonly SoCacheService _soCache;

    public EconInventory(SoCacheService soCache)
    {
        _soCache = soCache;
    }

    public ulong CacheVersion(ulong steamId) => _soCache.VersionOf(SoCacheKey.Econ(steamId));

    public IReadOnlyList<CSOEconItem> Items(ulong steamId) =>
        _soCache.ObjectsOfType<CSOEconItem>(SoCacheKey.Econ(steamId), DotaSoCache.TypeEconItem)
            .Select(entry => entry.Value)
            .ToList();

    /// <summary>
    /// Puts an item definition in the player's inventory. The item id is derived
    /// from the account and the definition, so granting the same definition twice
    /// updates the one item instead of duplicating it — the client keys its
    /// armory on that id.
    /// </summary>
    public CSOEconItem Grant(ulong steamId, uint accountId, uint defIndex, uint quantity)
    {
        var itemId = ItemId(accountId, defIndex);
        if (!TryGetItem(steamId, itemId, out var item))
        {
            item = new CSOEconItem
            {
                Id = itemId,
                OriginalId = itemId,
                AccountId = accountId,
                DefIndex = defIndex,
                Level = 1,
                Quality = DefaultQuality,
                Origin = DefaultOrigin,
                Inventory = DefaultInventoryPosition
            };
        }

        item.Quantity = quantity == 0 ? 1 : quantity;
        Write(steamId, item);
        return item;
    }

    private static ulong ItemId(uint accountId, uint defIndex) => ((ulong)accountId << 32) | defIndex;

    public bool TryGetItem(ulong steamId, ulong itemId, out CSOEconItem item) =>
        _soCache.TryGetObject(SoCacheKey.Econ(steamId), KeyOf(itemId), out item);

    /// <summary>
    /// Applies the client's equip requests and returns how many items actually
    /// changed. Equipping a slot that another item occupies unequips that other
    /// item, exactly like the real GC: the client renders one item per hero slot
    /// and would otherwise keep drawing both.
    /// </summary>
    public int Equip(ulong steamId, IEnumerable<CMsgAdjustItemEquippedState> adjustments)
    {
        var changed = 0;
        foreach (var adjustment in adjustments)
        {
            if (!TryGetItem(steamId, adjustment.ItemId, out var item))
            {
                continue;
            }

            var heroId = adjustment.NewClass;
            var slotId = adjustment.NewSlot;
            changed += Unequip(steamId, heroId, slotId, exceptItemId: adjustment.ItemId);

            var equipped = item.EquippedStates.FirstOrDefault(state => state.NewClass == heroId);
            if (equipped is not null && equipped.NewSlot == slotId && item.Style == Style(adjustment.StyleIndex))
            {
                continue;
            }

            if (equipped is null)
            {
                item.EquippedStates.Add(new CSOEconItemEquipped { NewClass = heroId, NewSlot = slotId });
            }
            else
            {
                equipped.NewSlot = slotId;
            }

            item.Style = Style(adjustment.StyleIndex);
            Write(steamId, item);
            changed++;
        }

        return changed;
    }

    public bool SetStyle(ulong steamId, ulong itemId, uint styleIndex)
    {
        if (!TryGetItem(steamId, itemId, out var item))
        {
            return false;
        }

        item.Style = Style(styleIndex);
        Write(steamId, item);
        return true;
    }

    /// <summary>
    /// Moves items around the inventory grid. The position is the low bits of
    /// <c>inventory</c>; the high bits carry flags the client sets, so they are
    /// preserved instead of overwritten.
    /// </summary>
    public int SetPositions(ulong steamId, IEnumerable<CMsgSetItemPositions.ItemPosition> positions)
    {
        var changed = 0;
        foreach (var position in positions)
        {
            if (!TryGetItem(steamId, position.ItemId, out var item) || item.Inventory == position.Position)
            {
                continue;
            }

            item.Inventory = (item.Inventory & 0xFFFF0000u) | (position.Position & 0x0000FFFFu);
            Write(steamId, item);
            changed++;
        }

        return changed;
    }

    private int Unequip(ulong steamId, uint heroId, uint slotId, ulong exceptItemId)
    {
        var changed = 0;
        var items = _soCache.ObjectsOfType<CSOEconItem>(SoCacheKey.Econ(steamId), DotaSoCache.TypeEconItem);

        foreach (var (key, item) in items)
        {
            if (key.Key == exceptItemId)
            {
                continue;
            }

            var occupied = item.EquippedStates.FirstOrDefault(state => state.NewClass == heroId && state.NewSlot == slotId);
            if (occupied is null)
            {
                continue;
            }

            item.EquippedStates.Remove(occupied);
            Write(steamId, item);
            changed++;
        }

        return changed;
    }

    private void Write(ulong steamId, CSOEconItem item) =>
        _soCache.Set(SoCacheKey.Econ(steamId), KeyOf(item.Id), item);

    private static SoObjectKey KeyOf(ulong itemId) => new(DotaSoCache.TypeEconItem, itemId);

    private static uint Style(uint styleIndex) => styleIndex == StyleDefaultSentinel ? StyleNone : styleIndex;
}
