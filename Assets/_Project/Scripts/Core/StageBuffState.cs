using System;
using System.Collections.Generic;

// Scene-owned runtime data; definitions are never mutated and nothing is saved globally.
public sealed class StageBuffState
{
    private readonly Dictionary<BuffType, int> _stacks = new Dictionary<BuffType, int>();
    private readonly Dictionary<BuffType, float> _totals = new Dictionary<BuffType, float>();

    public int GetStacks(BuffType type) => _stacks.TryGetValue(type, out int count) ? count : 0;
    public float GetTotal(BuffType type) => _totals.TryGetValue(type, out float total) ? total : 0f;

    public void Add(BuffType type, float amount)
    {
        float total = GetTotal(type) + amount;
        if (!Enum.IsDefined(typeof(BuffType), type) || float.IsNaN(amount) || float.IsInfinity(amount)
            || amount <= 0f || float.IsInfinity(total) || GetStacks(type) == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(amount), "Buff type and positive finite stack total must be valid.");
        _stacks[type] = GetStacks(type) + 1;
        _totals[type] = total;
    }
}
