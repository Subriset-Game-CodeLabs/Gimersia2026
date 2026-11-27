using UnityEngine;

// Per-spirit runtime state; scaled time expires even while the spirit is idle.
public sealed class SpiritItemStatus
{
    private float _hasteExpiry;
    private float _moveMultiplier = 1f;
    private float _attackMultiplier = 1f;
    private float _empowermentExpiry;
    private float _damageMultiplier = 1f;

    public bool IsHasted => IsFinite(Time.time) && Time.time >= 0f && Time.time < _hasteExpiry;
    public float MoveSpeedMultiplier => IsHasted ? _moveMultiplier : 1f;
    public float AttackSpeedMultiplier => IsHasted ? _attackMultiplier : 1f;
    public float HasteRemaining => IsHasted ? _hasteExpiry - Time.time : 0f;
    public bool IsEmpowered => IsFinite(Time.time) && Time.time >= 0f && Time.time < _empowermentExpiry;
    public bool IsActive => IsHasted || IsEmpowered;
    public float DamageMultiplier => IsEmpowered ? _damageMultiplier : 1f;
    public float EmpowermentRemaining => IsEmpowered ? _empowermentExpiry - Time.time : 0f;

    public bool TryApplyEmpowerment(float duration, float damageMultiplier)
    {
        float now = Time.time;
        float expiry = now + duration;
        if (!IsFinite(now) || now < 0f || !IsFinite(duration) || duration <= 0f
            || !IsFinite(expiry) || expiry <= now
            || !IsFinite(damageMultiplier) || damageMultiplier <= 1f) return false;

        _empowermentExpiry = expiry;
        _damageMultiplier = damageMultiplier;
        return true;
    }

    public bool TryApplyHaste(float duration, float moveMultiplier, float attackMultiplier)
    {
        float now = Time.time;
        float expiry = now + duration;
        if (!IsFinite(now) || now < 0f || !IsFinite(duration) || duration <= 0f
            || !IsFinite(expiry) || expiry <= now
            || !IsFinite(moveMultiplier) || moveMultiplier <= 1f
            || !IsFinite(attackMultiplier) || attackMultiplier <= 1f) return false;

        _hasteExpiry = expiry;
        _moveMultiplier = moveMultiplier;
        _attackMultiplier = attackMultiplier;
        return true;
    }

    public void Clear()
    {
        _hasteExpiry = 0f;
        _moveMultiplier = 1f;
        _attackMultiplier = 1f;
        _empowermentExpiry = 0f;
        _damageMultiplier = 1f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
