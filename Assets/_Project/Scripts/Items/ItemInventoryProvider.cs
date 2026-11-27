using System;
using System.Collections.Generic;
using UnityEngine;

// Scene adapter. Shop ownership is injected before any bag initialization.
public sealed class ItemInventoryProvider : MonoBehaviour
{
    [Serializable]
    public sealed class StartingItem
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField, Min(0)] private int _quantity;
        public ItemDefinition Item => _item;
        public int Quantity => _quantity;
    }

    [SerializeField] private ItemDefinition[] _catalog = Array.Empty<ItemDefinition>();
    [Tooltip("Development only. Ignored when an inventory is injected by the shop/bootstrap.")]
    [SerializeField] private bool _useTestStock;
    [SerializeField] private StartingItem[] _testStock = Array.Empty<StartingItem>();
    private ItemInventory _inventory = new ItemInventory();
    private bool _injected;
    private bool _initialized;
    public ItemInventory Inventory => _inventory;
    public IReadOnlyList<ItemDefinition> Catalog => Array.AsReadOnly(_catalog);
    public bool IsInitialized => _initialized;

    private void Start() => EnsureInitialized();

    public bool TrySetInventory(ItemInventory inventory)
    {
        if (_initialized || inventory == null) return false;
        _inventory = inventory;
        _injected = true;
        return true;
    }

    public bool EnsureInitialized()
    {
        if (_initialized) return true;
        // Resolve here, not just in Start: BagHUD may initialize this provider first.
        if (!_injected && ShopRuntime.Progress != null)
            TrySetInventory(ShopRuntime.Progress.Inventory);
        if (!TryValidate(out string error))
        {
            Debug.LogError("Item inventory: " + error, this);
            return false;
        }
        if (!_injected && _useTestStock)
        {
            GrantTestStock();
        }
        _initialized = true;
        return true;
    }

    public ItemDefinition FindDefinition(string id)
    {
        if (_catalog == null) return null;
        for (int i = 0; i < _catalog.Length; i++)
            if (_catalog[i] != null && string.Equals(_catalog[i].Id, id, StringComparison.Ordinal)) return _catalog[i];
        return null;
    }

    public bool TryValidate(out string error)
    {
        if (!TryValidateCatalog(out error)) return false;
        if (_injected || !_useTestStock) return true;

        return TryValidateTestStock(out error);
    }

    private bool TryValidateCatalog(out string error)
    {
        error = null;
        if (_catalog == null || _catalog.Length == 0)
        {
            error = "Assign an item catalog.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _catalog.Length; i++)
        {
            ItemDefinition definition = _catalog[i];
            if (definition == null)
            {
                error = $"Catalog entry {i} is missing.";
                return false;
            }

            if (!definition.TryValidate(out error)) return false;
            if (!ids.Add(definition.Id))
            {
                error = $"Duplicate item ID '{definition.Id}'.";
                return false;
            }
        }

        return true;
    }

    private bool TryValidateTestStock(out string error)
    {
        error = null;
        if (_testStock == null)
        {
            error = "Test stock must not be null.";
            return false;
        }

        var totals = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int i = 0; i < _testStock.Length; i++)
        {
            StartingItem entry = _testStock[i];
            if (!IsValidStartingItem(entry))
            {
                error = $"Test stock entry {i} needs a catalog item and nonnegative quantity.";
                return false;
            }

            totals.TryGetValue(entry.Item.Id, out long total);
            total += entry.Quantity;
            if (total > int.MaxValue - _inventory.GetQuantity(entry.Item.Id))
            {
                error = "Test stock exceeds inventory capacity.";
                return false;
            }

            totals[entry.Item.Id] = total;
        }

        return true;
    }

    private bool IsValidStartingItem(StartingItem entry)
    {
        return entry != null && entry.Item != null && entry.Quantity >= 0
            && FindDefinition(entry.Item.Id) == entry.Item;
    }

    private void GrantTestStock()
    {
        // Validate the whole stock before entering this grant step.
        foreach (StartingItem entry in _testStock)
        {
            if (entry.Quantity > 0) _inventory.TryAdd(entry.Item.Id, entry.Quantity);
        }
    }
}
