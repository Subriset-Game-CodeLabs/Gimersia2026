using System;
using System.Collections.Generic;

// Runtime ownership only. Item IDs are ordinal and are never normalized.
public sealed class ItemInventory
{
    private readonly Dictionary<string, int> _quantities = new Dictionary<string, int>(StringComparer.Ordinal);
    public event Action<string, int, int> OnQuantityChanged;

    public int GetQuantity(string id) => !string.IsNullOrWhiteSpace(id) && _quantities.TryGetValue(id, out int value) ? value : 0;

    // Detached snapshot for persistence; callers cannot mutate runtime ownership.
    public Dictionary<string, int> CaptureQuantities() => new Dictionary<string, int>(_quantities, StringComparer.Ordinal);

    public bool TryAdd(string id, int amount)
    {
        if (string.IsNullOrWhiteSpace(id) || amount <= 0) return false;

        int current = GetQuantity(id);
        if (current > int.MaxValue - amount) return false;

        SetQuantity(id, current + amount);
        return true;
    }

    public bool TryConsume(string id, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(id) || amount <= 0) return false;

        int current = GetQuantity(id);
        if (current < amount) return false;

        SetQuantity(id, current - amount);
        return true;
    }

    private void SetQuantity(string id, int quantity)
    {
        int before = GetQuantity(id);
        if (quantity == 0) _quantities.Remove(id);
        else _quantities[id] = quantity;
        OnQuantityChanged?.Invoke(id, before, quantity);
    }
}
