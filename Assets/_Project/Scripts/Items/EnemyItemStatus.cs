using System;

// Runtime-only status. The caller supplies scaled gameplay time.
public sealed class EnemyItemStatus
{
    private float _freezeUntil;
    private bool _hasFreeze;

    public bool TryFreeze(float duration, float now)
    {
        if (!IsFinite(duration) || duration <= 0f || !IsFinite(now) || now < 0f) return false;
        float expiry = now + duration;
        if (!IsFinite(expiry) || expiry <= now) return false;

        _freezeUntil = expiry;
        _hasFreeze = true;
        return true;
    }

    public bool IsFrozen(float now) => _hasFreeze && now < _freezeUntil;

    public float GetFreezeRemaining(float now) => IsFrozen(now) ? Math.Max(0f, _freezeUntil - now) : 0f;

    public void Clear()
    {
        _hasFreeze = false;
        _freezeUntil = 0f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
