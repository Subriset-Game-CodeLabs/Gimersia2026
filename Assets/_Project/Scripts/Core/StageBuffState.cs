using System;
using System.Collections.Generic;

// Scene-owned runtime data; definitions are never mutated and nothing is saved globally.
public sealed class StageBuffState
{
    private readonly Dictionary<BuffType, int> _stacks = new Dictionary<BuffType, int>();
    private readonly Dictionary<BuffType, float> _totals = new Dictionary<BuffType, float>();
    private readonly Dictionary<string, int> _cards = new Dictionary<string, int>(StringComparer.Ordinal);
    private float _specialChancePoints;

    public int GetCardStacks(string id) => _cards.TryGetValue(id, out int count) ? count : 0;

    private float NextTotal(BuffEffect effect, BuffCapSettings caps) => (float)Math.Min(
        (double)GetTotal(effect.Type) + effect.Amount, caps != null ? caps.LimitFor(effect.Type) : BuffCapSettings.SafetyLimitFor(effect.Type));

    public bool CanApply(BuffDefinition card, BuffCapSettings caps)
    {
        if (card == null || !card.TryValidate(out _)) return false;
        int count = GetCardStacks(card.Id);
        if (count == int.MaxValue || (card.MaxStacks > 0 && count >= card.MaxStacks)) return false;
        for (int i = 0; i < card.EffectCount; i++)
        {
            BuffEffect effect = card.EffectAt(i);
            if (NextTotal(effect, caps) > GetTotal(effect.Type) && GetStacks(effect.Type) < int.MaxValue) return true;
        }
        return false;
    }

    public bool TryApply(BuffDefinition card, BuffCapSettings caps)
    {
        if (!CanApply(card, caps)) return false;
        for (int i = 0; i < card.EffectCount; i++)
        {
            BuffEffect effect = card.EffectAt(i);
            float next = NextTotal(effect, caps);
            if (next <= GetTotal(effect.Type) || GetStacks(effect.Type) == int.MaxValue) continue;
            if (effect.Type == BuffType.EnemySpecialChanceReduction
                && effect.ChanceReductionMode == BuffChanceReductionMode.PercentagePoints)
                _specialChancePoints += next - GetTotal(effect.Type);
            _totals[effect.Type] = next;
            _stacks[effect.Type] = GetStacks(effect.Type) + 1;
        }
        _cards[card.Id] = GetCardStacks(card.Id) + 1;
        return true;
    }

    public int GetStacks(BuffType type) => _stacks.TryGetValue(type, out int count) ? count : 0;
    public float GetTotal(BuffType type) => _totals.TryGetValue(type, out float total) ? total : 0f;
    public float Multiplier(BuffType type) => 1f + GetTotal(type);
    public float RemainingFraction(BuffType type) => (float)Math.Max(0d, 1d - GetTotal(type));
    // Mixed modes share the stat cap's cumulative magnitude; relative reduction precedes point subtraction.
    public float EnemySpecialChance(float baseChance) => (float)Math.Max(0d,
        Math.Min(1d, baseChance) * Math.Max(0d, 1d - (GetTotal(BuffType.EnemySpecialChanceReduction) - _specialChancePoints))
        - _specialChancePoints);
    public float SpiritDamage(float baseDamage) => (baseDamage + GetTotal(BuffType.SpiritDamage)) * Multiplier(BuffType.SpiritDamageFraction);

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
