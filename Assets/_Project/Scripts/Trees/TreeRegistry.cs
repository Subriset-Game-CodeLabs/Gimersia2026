using System;
using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(-100)]
public class TreeRegistry : MonoBehaviour
{
    public static TreeRegistry Instance { get; private set; }
    [SerializeField] private ElementSettings _elementSettings;

    private readonly List<TreeNode> _trees = new List<TreeNode>();
    public ElementSettings Settings => _elementSettings;
    // A tree died. Used by the lose check and later by effects.
    public event Action<TreeNode> OnTreeDied;
    // The number of targetable trees changed (revealed, hidden, or died).
    public event Action OnCountChanged;
    public event Action OnPowerChanged;
    private void Awake()
    {
        Instance = this;
        if (_elementSettings == null)
            Debug.LogError("TreeRegistry needs ElementSettings assigned.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(TreeNode tree)
    {
        _trees.Add(tree);
        tree.OnAvailabilityChanged += HandleAvailabilityChanged;
        tree.Data.OnElementChanged += HandlePowerChanged;
        tree.Data.OnDied += () => OnTreeDied?.Invoke(tree);
    }

    // Trees that are revealed and alive. Small list, so a loop is cheap.
    public int AliveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _trees.Count; i++)
                if (_trees[i].IsTargetable) count++;
            return count;
        }
    }

    public int GetTotalPower(ElementType element)
    {
        if (_elementSettings == null || element == ElementType.None) return 0;
        int power = _elementSettings.BasePower;
        for (int i = 0; i < _trees.Count; i++)
        {
            TreeNode tree = _trees[i];
            if (tree.IsTargetable &&
                tree.Data.IsFullyGrown &&
                tree.Data.Element == element)
            {
                power += tree.Data.Definition.ElementPower;
            }
        }
        return power;
    }

    public float GetAttackSpeedMultiplier(ElementType element)
    {
        if (_elementSettings == null || element == ElementType.None) return 1f;
        return _elementSettings.GetAttackSpeedMultiplier(GetTotalPower(element));
    }
    public float GetGrowthTimeMultiplier(ElementType element)
    {
        if (_elementSettings == null || element == ElementType.None) return 1f;
        return _elementSettings.GetGrowthTimeMultiplier(GetTotalPower(element));
    }

    // Nearest targetable tree on the ground plane. No allocations.
    public bool TryGetNearest(Vector3 from, out TreeNode nearest)
    {
        nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < _trees.Count; i++)
        {
            TreeNode tree = _trees[i];
            if (!tree.IsTargetable) continue;

            Vector3 d = tree.TargetPoint - from;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr < best)
            {
                best = sqr;
                nearest = tree;
            }
        }
        return nearest != null;
    }

    private void HandleAvailabilityChanged(TreeNode tree)
    {
        OnCountChanged?.Invoke();
        OnPowerChanged?.Invoke();
    }
    private void HandlePowerChanged()
    {
        OnPowerChanged?.Invoke();
    }

}
