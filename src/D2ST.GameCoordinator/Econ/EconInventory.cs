using D2ST.Core.Accounts;
using D2ST.GameCoordinator.SharedObjects;
using D2ST.Protocol.Dota;

namespace D2ST.GameCoordinator.Econ;

/// <summary>
/// The player's items are persisted in the API's local economy and projected
/// into the econ Shared Object cache: the client already mirrors that cache, so
/// writing an item through <see cref="SoCacheService"/> publishes the delta
/// that makes the armory redraw.
/// <para>
/// The API store owns catalog validation, wallet mutations and durable item
/// rows. This class keeps the GC assembly independent of EF while ensuring
/// reconnects rebuild the volatile cache from those rows.
/// </para>
/// </summary>
public sealed class EconInventory
{
    /// <summary>Styles are one byte; 255 is the client's "no style" sentinel.</summary>
    private const uint StyleNone = 0;
    private const uint StyleDefaultSentinel = 255;

    private readonly SoCacheService _soCache;
    private readonly IEconomyStore _economy;

    public EconInventory(SoCacheService soCache, IEconomyStore economy)
    {
        _soCache = soCache;
        _economy = economy;
    }

    public ulong CacheVersion(ulong steamId) => _soCache.VersionOf(SoCacheKey.Econ(steamId));

    public IReadOnlyList<CSOEconItem> Items(ulong steamId)
    {
        var cached = _soCache.ObjectsOfType<CSOEconItem>(SoCacheKey.Econ(steamId), DotaSoCache.TypeEconItem)
            .Select(entry => entry.Value)
            .ToList();
        if (cached.Count != 0)
        {
            return cached;
        }

        EnsureCache(steamId, SteamAccount.AccountIdFromSteamId(steamId));
        return _soCache.ObjectsOfType<CSOEconItem>(SoCacheKey.Econ(steamId), DotaSoCache.TypeEconItem)
            .Select(entry => entry.Value)
            .ToList();
    }

    /// <summary>
    /// Hydrates the volatile econ cache from the durable inventory on logon or
    /// after a purchase. Empty inventories still declare their SO type.
    /// </summary>
    public void EnsureCache(ulong steamId, uint accountId)
    {
        var key = SoCacheKey.Econ(steamId);
        var items = _economy.GetItems(accountId);
        foreach (var item in items)
        {
            _soCache.SetIfChanged(key, KeyOf(item.Id), item);
        }

        if (items.Count == 0)
        {
            _soCache.DeclareEmptyType(key, DotaSoCache.TypeEconItem);
        }
    }

    public void ApplyItems(ulong steamId, uint accountId, IEnumerable<CSOEconItem> items)
    {
        var key = SoCacheKey.Econ(steamId);
        var any = false;
        foreach (var item in items)
        {
            if (item.AccountId != 0 && item.AccountId != accountId)
            {
                continue;
            }

            any = true;
            _soCache.Set(key, KeyOf(item.Id), item);
        }

        if (!any)
        {
            _soCache.DeclareEmptyType(key, DotaSoCache.TypeEconItem);
        }
    }

    public StoreOperationResult Purchase(
        uint accountId,
        ulong steamId,
        IReadOnlyList<StorePurchaseLine> lines)
    {
        var result = _economy.Purchase(accountId, lines);
        if (result.Success)
        {
            ApplyItems(steamId, accountId, result.Items);
        }

        return result;
    }

    /// <summary>
    /// Puts an item definition in the player's inventory. The item id is derived
    /// from the account and the definition, so granting the same definition twice
    /// updates the one item instead of duplicating it — the client keys its
    /// armory on that id.
    /// </summary>
    public CSOEconItem Grant(ulong steamId, uint accountId, uint defIndex, uint quantity)
    {
        var item = _economy.GrantItem(accountId, defIndex, quantity);
        Write(steamId, item);
        return item;
    }

    public bool TryGetItem(ulong steamId, ulong itemId, out CSOEconItem item) =>
        _soCache.TryGetObject(SoCacheKey.Econ(steamId), KeyOf(itemId), out item)
        || TryHydrateItem(steamId, itemId, out item);

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

    private void Write(ulong steamId, CSOEconItem item)
    {
        var accountId = SteamAccount.AccountIdFromSteamId(steamId);
        _economy.SaveItem(accountId, item);
        _soCache.Set(SoCacheKey.Econ(steamId), KeyOf(item.Id), item);
    }

    private bool TryHydrateItem(ulong steamId, ulong itemId, out CSOEconItem item)
    {
        var accountId = SteamAccount.AccountIdFromSteamId(steamId);
        var found = _economy.GetItems(accountId).FirstOrDefault(candidate => candidate.Id == itemId);
        if (found is null)
        {
            item = default!;
            return false;
        }

        item = found;
        _soCache.SetIfChanged(SoCacheKey.Econ(steamId), KeyOf(item.Id), item);
        return true;
    }

    private static SoObjectKey KeyOf(ulong itemId) => new(DotaSoCache.TypeEconItem, itemId);

    private static uint Style(uint styleIndex) => styleIndex == StyleDefaultSentinel ? StyleNone : styleIndex;
}
