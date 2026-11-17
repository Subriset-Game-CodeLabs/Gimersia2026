using System;

public class TreeData
{
    public TreeDefinition Definition { get; }
    public ElementType Element { get; private set; } = ElementType.None;
    public TreeStage Stage { get; private set; } = TreeStage.Initial;
    public float Hp { get; private set; }

    public bool IsAlive => Hp > 0f;
    public bool IsFullyGrown => Stage == TreeStage.Complete;

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

    public void SetElement(ElementType element)
    {
        if (Element == element) return;
        Element = element;
        OnElementChanged?.Invoke();
    }
}