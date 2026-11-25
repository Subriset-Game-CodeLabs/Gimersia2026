using System;
using System.Collections.Generic;

// Coins and ownership are saved together; unknown IDs survive catalog changes.
[Serializable]
public sealed class ShopSaveData
{
    public int version = 1;
    public int coins;
    public List<ItemCount> items = new List<ItemCount>();

    [Serializable]
    public sealed class ItemCount
    {
        public string id;
        public int quantity;
    }

    public bool IsValid()
    {
        if (version != 1 || coins < 0 || items == null || items.Count > 10000) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ItemCount item in items)
            if (item == null || string.IsNullOrWhiteSpace(item.id) || item.id.Length > 256
                || item.quantity <= 0 || !ids.Add(item.id)) return false;
        return true;
    }
}
