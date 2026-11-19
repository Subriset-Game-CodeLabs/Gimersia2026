using System;
using UnityEngine;

public class TreeData
{
    public TreeDefinition Definition { get; }
    public ElementType Element { get; private set; } = ElementType.None;
    public TreeStage Stage { get; private set; } = TreeStage.Initial;
    public float Hp { get; private set; }

    public bool IsAlive => Hp > 0f;
    public bool IsFullyGrown => Stage == TreeStage.Complete;
    public bool NeedsHealing => IsAlive && Hp < Definition.MaxHp;

    public float ElementCooldownRemaining =>
            Element == ElementType.None ? 0f : Mathf.Max(0f, _nextElementChangeTime - Time.time);
    private float _nextElementChangeTime;

    // Events instead of polling, so views and later systems just subscribe.
    public event Action OnStageChanged;
    public event Action OnElementChanged;

    public TreeData(TreeDefinition definition)
    {
        Definition = definition;
        Hp = definition.MaxHp;
    }

    public void SetStage(TreeStage stage)
    {
        if (Stage == stage) return;
        Stage = stage;
        OnStageChanged?.Invoke();
    }

    public bool TryAssignElement(ElementType element, ElementSettings settings)
    {
        if (settings == null || !IsAlive || !IsFullyGrown ||
            Element != ElementType.None || element == ElementType.None)
            return false;
        Element = element;
        OnElementChanged?.Invoke();
        return true;
    }

    public event Action OnDied;
    public event Action OnHpChanged; // ready for a health bar later

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        Hp = Mathf.Max(0f, Hp - amount);
        OnHpChanged?.Invoke();
        if (!IsAlive) OnDied?.Invoke();
    }

    public void Heal(float amount)
    {
        if (!NeedsHealing || amount <= 0f) return;
        Hp = Mathf.Min(Definition.MaxHp, Hp + amount);
        OnHpChanged?.Invoke();
    }

    public bool CompleteReassignment(ElementType element)
    {
        if (!IsAlive || !IsFullyGrown ||
            Element == ElementType.None ||
            element == ElementType.None || element == Element)
            return false;
        Element = element;
        OnElementChanged?.Invoke();
        return true;
    }
}