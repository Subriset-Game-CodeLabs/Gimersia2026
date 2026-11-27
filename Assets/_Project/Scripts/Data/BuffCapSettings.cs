using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffCaps", menuName = "Game/Buff Stat Caps")]
public sealed class BuffCapSettings : ScriptableObject
{
    [Tooltip("Maximum cumulative magnitude per stat. Omitted bonuses are uncapped; enemy reductions have a 100% safety ceiling. Chance modes share one magnitude cap. TODO(balance): configure for this level.")]
    [SerializeField] private BuffEffect[] _limits = Array.Empty<BuffEffect>();

    public float LimitFor(BuffType type)
    {
        float limit = SafetyLimitFor(type);
        if (_limits != null)
            foreach (BuffEffect entry in _limits)
                if (entry.Type == type && entry.IsValid) limit = Math.Min(limit, entry.Amount);
        return limit;
    }

    // Mathematical bounds, not balance defaults. Designer caps belong in Limits.
    public static float SafetyLimitFor(BuffType type)
    {
        switch (type)
        {
            case BuffType.EnemyMoveSpeedReduction:
            case BuffType.EnemyMaxHpReduction:
            case BuffType.EnemyDamageReduction:
            case BuffType.EnemyAttackSpeedReduction:
            case BuffType.EnemySpecialChanceReduction:
                return 1f;
            default: return float.MaxValue;
        }
    }
}
