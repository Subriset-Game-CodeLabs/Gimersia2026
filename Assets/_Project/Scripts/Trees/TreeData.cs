using System;
using UnityEngine;

public enum TreeDowngradeStep
{
    ElementToComplete,
    CompleteToIntermediate,
    IntermediateToInitial
}

public class TreeData
{
    public const float DefaultTierMaxHp = 100f;

    public TreeDefinition Definition { get; }
    public ElementType Element { get; private set; } = ElementType.None;
    public TreeStage Stage { get; private set; } = TreeStage.Initial;
    public float Hp { get; private set; }

    public float MaxHp
    {
        get
        {
            if (Stage == TreeStage.Initial) return 0f;
            return (Definition != null && Definition.MaxHp > 0f) ? Definition.MaxHp : DefaultTierMaxHp;
        }
    }

    public bool HasElement => Stage == TreeStage.Complete && Element != ElementType.None;
    // Untouched Initial nodes are growable; a destroyed node can never grow again.
    public bool IsAlive { get; private set; } = true;
    public bool IsVulnerableToEnemies => IsAlive && Stage >= TreeStage.Intermediate && Hp > 0f;
    public bool IsFullyGrown => IsAlive && Stage == TreeStage.Complete;
    public bool NeedsHealing => IsVulnerableToEnemies && Hp < MaxHp;

    public float ElementCooldownRemaining =>
        Element == ElementType.None ? 0f : Mathf.Max(0f, _nextElementChangeTime - Time.time);
    private float _nextElementChangeTime;

    private float _shieldExpiryTime;
    public bool IsShielded => _shieldExpiryTime > 0f && IsValidClock(Time.time) && Time.time < _shieldExpiryTime;
    public float ShieldRemaining => IsShielded ? _shieldExpiryTime - Time.time : 0f;

    public bool TryApplyShield(float duration)
    {
        float now = Time.time;
        if (!IsVulnerableToEnemies || !IsPositiveFinite(duration) || !IsValidClock(now)) return false;

        // Reapply sets a fresh expiry from the same scaled clock; durations never stack.
        float expiry = now + duration;
        if (!IsPositiveFinite(expiry) || expiry <= now) return false;

        _shieldExpiryTime = expiry;
        return true;
    }

    public void ClearItemStatus()
    {
        _shieldExpiryTime = 0f;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsValidClock(float now) => now >= 0f && !float.IsNaN(now) && !float.IsInfinity(now);

    public event Action OnStageChanged;
    public event Action OnElementChanged;
    public event Action OnStageUpgraded;
    public event Action OnStageDowngraded;
    public event Action OnDied;
    public event Action OnHpChanged;
    public event Action<float, Vector3> OnDamaged;
    public event Action<EnemyController, float> OnDamagedByEnemy;
    public event Action<TreeDowngradeStep, ElementType, EnemyController, Vector3> OnDowngradedByDamage;

    public TreeData(TreeDefinition definition)
    {
        Definition = definition;
        Stage = TreeStage.Initial;
        Hp = 0f;
    }

    public void SetStage(TreeStage stage)
    {
        if (!IsAlive || Stage == stage) return;

        bool isUpgrade = stage > Stage;
        Stage = stage;

        if (Stage == TreeStage.Initial)
        {
            Element = ElementType.None;
            Hp = 0f;
        }
        else
        {
            Hp = MaxHp;
        }

        OnStageChanged?.Invoke();
        OnHpChanged?.Invoke();

        if (isUpgrade)
            OnStageUpgraded?.Invoke();
        else
            OnStageDowngraded?.Invoke();
    }

    public bool TryAssignElement(ElementType element, ElementSettings settings)
    {
        if (settings == null || !IsFullyGrown ||
            Element != ElementType.None || element == ElementType.None)
            return false;

        Element = element;
        Hp = MaxHp;

        OnElementChanged?.Invoke();
        OnHpChanged?.Invoke();
        OnStageUpgraded?.Invoke();
        return true;
    }

    public bool CompleteReassignment(ElementType element)
    {
        if (!IsFullyGrown ||
            Element == ElementType.None ||
            element == ElementType.None || element == Element)
            return false;

        Element = element;
        Hp = MaxHp;

        OnElementChanged?.Invoke();
        OnHpChanged?.Invoke();
        OnStageUpgraded?.Invoke();
        return true;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector3.zero, null);
    }

    public void TakeDamage(float amount, Vector3 hitDirection)
    {
        TakeDamage(amount, hitDirection, null);
    }

    public void TakeDamage(float amount, EnemyController attacker)
    {
        TakeDamage(amount, Vector3.zero, attacker);
    }

    private void TakeDamage(float amount, Vector3 hitDirection, EnemyController attacker)
    {
        if (!IsVulnerableToEnemies || IsShielded || amount <= 0f) return;

        if (hitDirection == Vector3.zero && attacker != null)
        {
            hitDirection = attacker.transform.forward;
        }

        Hp = Mathf.Max(0f, Hp - amount);
        if (Hp == 0f && Stage == TreeStage.Intermediate)
        {
            // Commit death before callbacks: a retaliation can synchronously clear the wave.
            IsAlive = false;
            ClearItemStatus();
        }
        OnHpChanged?.Invoke();
        OnDamaged?.Invoke(amount, hitDirection);
        if (attacker != null)
            OnDamagedByEnemy?.Invoke(attacker, amount);

        if (Hp > 0f) return;

        if (Stage == TreeStage.Complete && Element != ElementType.None)
        {
            ElementType previousElement = Element;
            Element = ElementType.None;
            Hp = MaxHp;

            OnElementChanged?.Invoke();
            OnHpChanged?.Invoke();
            OnStageDowngraded?.Invoke();
            OnDowngradedByDamage?.Invoke(TreeDowngradeStep.ElementToComplete, previousElement, attacker, hitDirection);
        }
        else if (Stage == TreeStage.Complete)
        {
            Stage = TreeStage.Intermediate;
            Hp = MaxHp;

            OnStageChanged?.Invoke();
            OnHpChanged?.Invoke();
            OnStageDowngraded?.Invoke();
            OnDowngradedByDamage?.Invoke(TreeDowngradeStep.CompleteToIntermediate, ElementType.None, attacker, hitDirection);
        }
        else if (Stage == TreeStage.Intermediate)
        {
            // Keep the existing downgrade feedback step, but Initial no longer means regrowable here.
            Stage = TreeStage.Initial;
            Element = ElementType.None;
            Hp = 0f;

            OnStageChanged?.Invoke();
            OnHpChanged?.Invoke();
            OnStageDowngraded?.Invoke();
            OnDowngradedByDamage?.Invoke(TreeDowngradeStep.IntermediateToInitial, ElementType.None, attacker, hitDirection);
            OnDied?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (!NeedsHealing || amount <= 0f) return;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        OnHpChanged?.Invoke();
    }
}