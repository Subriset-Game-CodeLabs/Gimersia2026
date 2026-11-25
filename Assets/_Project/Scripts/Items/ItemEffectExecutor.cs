using System;
using System.Collections.Generic;
using UnityEngine;

// Area effects use an enemy snapshot; single-tree effects require an explicit target.
public sealed class ItemEffectExecutor
{
    private readonly List<EnemyController> _targets = new List<EnemyController>();
    private readonly HashSet<EnemyController> _seen = new HashSet<EnemyController>();
    private readonly List<TreeNode> _shieldTargets = new List<TreeNode>();
    private readonly HashSet<TreeNode> _seenTrees = new HashSet<TreeNode>();
    private bool _applying;
    private readonly List<SpiritController> _spiritTargets = new List<SpiritController>();
    private readonly HashSet<SpiritController> _seenSpirits = new HashSet<SpiritController>();

    public static bool Supports(ItemDefinition item) => item != null
        && (item.Effect == ItemEffectType.AreaDamage || item.Effect == ItemEffectType.AreaFreeze
            || item.Effect == ItemEffectType.InstantGrowth || item.Effect == ItemEffectType.TreeInvulnerability
            || item.TargetsTeam);

    public bool TryApplyTeam(ItemDefinition item, ItemInventory inventory, Func<bool> canUse)
    {
        if (_applying || item == null || !item.TargetsTeam
            || inventory == null || canUse == null || !item.TryValidate(out _) || !canUse()) return false;

        _applying = true;
        try
        {
            string id = item.Id;
            float duration = item.Duration;
            float move = item.MoveSpeedMultiplier;
            float attack = item.AttackSpeedMultiplier;
            float damage = item.DamageMultiplier;
            ItemEffectType effect = item.Effect;
            foreach (SpiritController spirit in SpiritController.RegisteredSpirits)
                if (spirit != null && spirit.CanReceiveTeamItem && _seenSpirits.Add(spirit)) _spiritTargets.Add(spirit);
            if (_spiritTargets.Count == 0 || !inventory.TryConsume(id)) return false;

            int applied = 0;
            foreach (SpiritController spirit in _spiritTargets)
            {
                if (!canUse()) break;
                if (spirit == null || !spirit.CanReceiveTeamItem) continue;
                bool success = effect == ItemEffectType.TeamHaste
                    ? spirit.TryApplyHaste(duration, move, attack)
                    : spirit.TryApplyEmpowerment(duration, damage);
                if (success) applied++;
            }
            if (applied == 0) inventory.TryAdd(id, 1);
            return applied > 0;
        }
        finally
        {
            _spiritTargets.Clear();
            _seenSpirits.Clear();
            _applying = false;
        }
    }

    public bool HasTargets(ItemDefinition item, Vector3 center)
    {
        if (!Supports(item) || !item.TargetsArea || !IsFinite(center)) return false;

        if (item.Effect == ItemEffectType.TreeInvulnerability)
        {
            TreeRegistry registry = TreeRegistry.Instance;
            if (registry == null) return false;
            foreach (TreeNode tree in registry.Trees)
            {
                if (IsEligible(tree, center, item.Radius)) return true;
            }
            return false;
        }

        foreach (EnemyController enemy in EnemyController.ActiveEnemies)
        {
            if (IsEligible(enemy, center, item.Radius)) return true;
        }
        return false;
    }

    public bool TryApply(ItemDefinition item, ItemInventory inventory, Vector3 center, Func<bool> canUse)
    {
        if (_applying || !Supports(item) || !item.TargetsArea || inventory == null || canUse == null) return false;
        if (!item.TryValidate(out _) || !IsFinite(center) || !canUse()) return false;

        _applying = true;
        try
        {
            if (item.Effect == ItemEffectType.TreeInvulnerability)
                return TryApplyShieldArea(item, inventory, center, canUse);

            float radius = item.Radius;
            ItemEffectType effect = item.Effect;
            float damage = item.Damage;
            float duration = item.Duration;
            string id = item.Id;
            CollectTargets(center, radius);
            if (_targets.Count == 0 || !inventory.TryConsume(id)) return false;

            // Reserve before damage: a kill can synchronously finish a wave.
            int applied = 0;
            foreach (EnemyController enemy in _targets)
            {
                if (!canUse()) break;
                if (!IsEligible(enemy, center, radius)) continue;

                switch (effect)
                {
                    case ItemEffectType.AreaDamage:
                        applied++;
                        enemy.TakeDamage(damage, center);
                        break;
                    case ItemEffectType.AreaFreeze:
                        if (enemy.TryFreeze(duration)) applied++;
                        break;
                }
            }

            if (applied == 0) inventory.TryAdd(id, 1);
            return applied > 0;
        }
        finally
        {
            _targets.Clear();
            _seen.Clear();
            _shieldTargets.Clear();
            _seenTrees.Clear();
            _applying = false;
        }
    }

    public bool HasTreeTarget(ItemDefinition item, TreeInteractable tree)
    {
        return item != null && item.Effect == ItemEffectType.InstantGrowth
            && tree != null && tree.isActiveAndEnabled && tree.CanGrowInstantly;
    }

    public bool TryApplyTree(ItemDefinition item, ItemInventory inventory, TreeInteractable tree, Func<bool> canUse)
    {
        if (_applying || inventory == null || canUse == null || !HasTreeTarget(item, tree)) return false;
        if (!item.TryValidate(out _) || !canUse()) return false;

        _applying = true;
        try
        {
            string id = item.Id;
            if (!inventory.TryConsume(id)) return false;

            // Stock listeners may cancel the session or invalidate the clicked tree.
            bool applied = canUse() && HasTreeTarget(item, tree) && tree.TryGrowInstantly();
            if (!applied) inventory.TryAdd(id, 1);
            return applied;
        }
        finally
        {
            _applying = false;
        }
    }

    private bool TryApplyShieldArea(ItemDefinition item, ItemInventory inventory, Vector3 center, Func<bool> canUse)
    {
        TreeRegistry registry = TreeRegistry.Instance;
        if (registry == null) return false;

        float radius = item.Radius;
        float duration = item.Duration;
        string id = item.Id;
        foreach (TreeNode tree in registry.Trees)
        {
            if (IsEligible(tree, center, radius) && _seenTrees.Add(tree)) _shieldTargets.Add(tree);
        }
        if (_shieldTargets.Count == 0 || !inventory.TryConsume(id)) return false;

        int applied = 0;
        foreach (TreeNode tree in _shieldTargets)
        {
            if (!canUse()) break;
            if (IsEligible(tree, center, radius) && tree.Data.TryApplyShield(duration)) applied++;
        }
        if (applied == 0) inventory.TryAdd(id, 1);
        return applied > 0;
    }

    private static bool IsEligible(TreeNode tree, Vector3 center, float radius)
    {
        return tree != null && tree.isActiveAndEnabled && tree.IsEnemyTargetable
            && IsInArea(tree.transform.position, center, radius);
    }

    private void CollectTargets(Vector3 center, float radius)
    {
        // Damage removes enemies from ActiveEnemies, so snapshot before applying.
        foreach (EnemyController enemy in EnemyController.ActiveEnemies)
        {
            if (IsEligible(enemy, center, radius) && _seen.Add(enemy)) _targets.Add(enemy);
        }
    }

    private static bool IsEligible(EnemyController enemy, Vector3 center, float radius)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsTargetableBySpirit) return false;
        return IsInArea(enemy.transform.position, center, radius);
    }

    private static bool IsInArea(Vector3 position, Vector3 center, float radius)
    {
        if (!IsFinite(position)) return false;

        double x = (double)position.x - center.x;
        double z = (double)position.z - center.z;
        return x * x + z * z <= (double)radius * radius;
    }

    private static bool IsFinite(Vector3 point)
    {
        return IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
