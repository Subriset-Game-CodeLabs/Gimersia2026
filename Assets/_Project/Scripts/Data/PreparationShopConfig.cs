using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PreparationShop", menuName = "Game/Preparation Shop Config")]
public sealed class PreparationShopConfig : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private ItemDefinition _item;
        // TODO(balance): configure prices with the design team; zero is not a valid price.
        [SerializeField, Min(1)] private int _price = 1;
        public ItemDefinition Item => _item;
        public int Price => _price;
    }

    [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
    // TODO(balance): development-only new-save allowance, not release currency.
    [SerializeField, Min(0)] private int _developmentInitialCoins = 1250;
    public IReadOnlyList<Entry> Entries => Array.AsReadOnly(_entries);
    public int InitialCoins => Application.isEditor || Debug.isDebugBuild ? _developmentInitialCoins : 0;

    public bool TryValidate(out string error)
    {
        error = null;
        if (_developmentInitialCoins < 0 || _entries == null || _entries.Length == 0)
            error = "Configure at least one item and nonnegative development starting coins.";
        else
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Entry entry in _entries)
            {
                if (entry == null || entry.Item == null || entry.Price <= 0)
                { error = "Every shop entry needs an item and positive price."; break; }
                if (!entry.Item.TryValidate(out error)) break;
                if (!ids.Add(entry.Item.Id)) { error = "Shop item IDs must be unique."; break; }
            }
        }
        return error == null;
    }
}
