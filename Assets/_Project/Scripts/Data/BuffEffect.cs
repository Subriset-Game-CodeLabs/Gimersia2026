using System;
using UnityEngine;

public enum BuffChanceReductionMode { Relative = 0, PercentagePoints = 1 }

[Serializable]
public struct BuffEffect
{
    [SerializeField] private BuffType _type;
    [Tooltip("Legacy Spirit Damage uses flat points. Other stats use fractions (0.1 = 10%).")]
    [SerializeField] private float _amount;
    [Tooltip("Enemy Special Chance Reduction only: Relative multiplies the original chance; Percentage Points subtracts Amount directly (0.08 = 8 points).")]
    [SerializeField] private BuffChanceReductionMode _chanceReductionMode;

    public BuffType Type => _type;
    public float Amount => _amount;
    public BuffChanceReductionMode ChanceReductionMode => _chanceReductionMode;
    public BuffEffect(BuffType type, float amount, BuffChanceReductionMode chanceReductionMode = BuffChanceReductionMode.Relative)
    { _type = type; _amount = amount; _chanceReductionMode = chanceReductionMode; }
    public bool IsValid => Enum.IsDefined(typeof(BuffType), _type) && _amount > 0f && !float.IsInfinity(_amount)
        && Enum.IsDefined(typeof(BuffChanceReductionMode), _chanceReductionMode);
}
