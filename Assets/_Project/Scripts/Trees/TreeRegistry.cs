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
    private PhaseManager _phases;
    private NightRewardController _rewards;
    private int _lastHealDay;

    private void Start()
    {
        _phases = PhaseManager.Instance;
        if (_phases == null) return;
        _phases.OnDayStart += HandleDayStart;
        _rewards = _phases.Rewards;
        if (_rewards == null) return;
        _rewards.OnCardChosen += HandleCardChosen;
        ApplyTreeBonuses(null); // Trees may have registered in Awake before phase setup.
    }

    private void HandleCardChosen(BuffDefinition card) => ApplyTreeBonuses(card);

    private void ApplyTreeBonuses(BuffDefinition card)
    {
        int count = _trees.Count;
        for (int i = 0; i < count; i++)
        {
            TreeNode tree = _trees[i];
            if (tree == null || tree.Data == null) continue;
            tree.Data.ApplyStageBonuses(_rewards.State);
            if (card != null && tree.IsTargetable)
                tree.Data.HealFraction(card.SelectionHealFraction, card.HealMissingHp);
        }
    }

    private void HandleDayStart(int day)
    {
        if (day <= _lastHealDay) return;
        _lastHealDay = day;
        float fraction = _phases.Buffs?.GetTotal(BuffType.TreeDayHeal) ?? 0f;
        if (fraction <= 0f) return;
        int count = _trees.Count;
        for (int i = 0; i < count; i++)
        {
            TreeNode tree = _trees[i];
            if (tree != null && tree.IsTargetable) tree.Data.HealFraction(fraction, false);
        }
    }

    private void Awake()
    {
        Instance = this;
        if (_elementSettings == null)
            Debug.LogError("TreeRegistry needs ElementSettings assigned.", this);
    }

    private void OnDestroy()
    {
        if (_phases != null) _phases.OnDayStart -= HandleDayStart;
        if (_rewards != null) _rewards.OnCardChosen -= HandleCardChosen;
        if (Instance == this) Instance = null;
    }

    public void Register(TreeNode tree)
    {
        if (tree == null || tree.Data == null || _trees.Contains(tree)) return;
        tree.Data.ApplyStageBonuses(_rewards != null ? _rewards.State : null);
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