using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class TreeRegistry : MonoBehaviour
{
    public static TreeRegistry Instance { get; private set; }
    [SerializeField] private ElementSettings _elementSettings;

    private readonly List<TreeNode> _trees = new List<TreeNode>();
    public IReadOnlyList<TreeNode> Trees => _trees;
    public ElementSettings Settings => _elementSettings;

    public event Action<TreeNode> OnTreeDied;
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

    // Survival/results count grown trees, not untouched growable Initial nodes.
    public int AliveCount => GetGrownTreeCount();

    /// <summary>
    /// Menghitung jumlah pohon yang sudah mencapai minimal fase Intermediate.
    /// Bertambah saat naik dari Initial -> Intermediate, dan berkurang saat turun kembali ke Initial.
    /// </summary>
    public int GetGrownTreeCount(bool includeComplete = true)
    {
        int count = 0;
        for (int i = 0; i < _trees.Count; i++)
        {
            TreeNode tree = _trees[i];
            if (tree == null || !tree.IsTargetable || tree.Data == null) continue;

            if (includeComplete)
            {
                if (tree.Data.Stage >= TreeStage.Intermediate)
                    count++;
            }
            else
            {
                if (tree.Data.Stage == TreeStage.Intermediate)
                    count++;
            }
        }
        return count;
    }

    public int GetTotalPower(ElementType element)
    {
        if (_elementSettings == null || element == ElementType.None) return 0;
        int power = _elementSettings.BasePower;
        for (int i = 0; i < _trees.Count; i++)
        {
            TreeNode tree = _trees[i];
            if (tree != null && tree.IsTargetable &&
                tree.Data.IsFullyGrown &&
                tree.Data.Element == element)
            {
                int bonus = tree.Data.Definition.ElementPower > 0
                    ? tree.Data.Definition.ElementPower
                    : (tree.Data.Definition.Type == TreeType.Tumbang ? 15 : 10);
                power += bonus;
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

    public bool TryGetNearest(Vector3 from, out TreeNode nearest)
    {
        nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < _trees.Count; i++)
        {
            TreeNode tree = _trees[i];
            if (tree == null || !tree.IsEnemyTargetable) continue;

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