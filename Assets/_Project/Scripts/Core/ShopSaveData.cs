using System;
using System.Collections.Generic;

// Coins and ownership are saved together; unknown IDs survive catalog changes.
[Serializable]
public sealed class ShopSaveData
{
    public int version = 2;
    public int coins;
    public List<ItemCount> items = new List<ItemCount>();
    public List<string> completedLevelIds = new List<string>();

    [Serializable]
    public sealed class ItemCount
    {
        public string id;
        public int quantity;
    }

    public bool IsValid()
    {
        if ((version != 1 && version != 2) || coins < 0 || items == null || items.Count > 10000) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ItemCount item in items)
            if (item == null || string.IsNullOrWhiteSpace(item.id) || item.id.Length > 256
                || item.quantity <= 0 || !ids.Add(item.id)) return false;
        // Version 1 had no completion field; its wallet and bag migrate on the next save.
        if (completedLevelIds == null) return version == 1;
        if (completedLevelIds.Count > 10000) return false;
        ids.Clear();
        foreach (string id in completedLevelIds)
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || !ids.Add(id)) return false;
        return true;
    }
}
