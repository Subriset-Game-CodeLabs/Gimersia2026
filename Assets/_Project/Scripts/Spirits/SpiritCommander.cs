using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SpiritCommander : MonoBehaviour
{
    [SerializeField] private SpiritSelection _selection;
    [SerializeField] private SpiritCombatSettings _combatSettings;
    [Tooltip("Radius toleransi klik di dekat musuh bergerak agar perintah tetap terbaca sebagai Attack.")]
    [SerializeField, Min(0f)] private float _enemyClickSnapRadius = 0.75f;

    private Bounds _cachedIslandBounds;
    private bool _hasCachedIslandBounds;

    public SpiritCombatSettings CombatSettings => ResolveCombatSettings();

    private SpiritCombatSettings ResolveCombatSettings()
    {
        if (_combatSettings != null) return _combatSettings;

        if (_selection != null && _selection.Selected.Count > 0)
        {
            for (int i = 0; i < _selection.Selected.Count; i++)
            {
                SpiritView sv = _selection.Selected[i];
                if (sv == null) continue;
                SpiritController sc = sv.GetComponent<SpiritController>();
                if (sc != null && sc.CombatSettings != null)
                {
                    _combatSettings = sc.CombatSettings;
                    return _combatSettings;
                }
            }
        }

        SpiritController anySpirit = FindFirstObjectByType<SpiritController>();
        if (anySpirit != null && anySpirit.CombatSettings != null)
            _combatSettings = anySpirit.CombatSettings;

        return _combatSettings;
    }

    public bool CanAttackEnemy(EnemyController enemy)
    {
        return enemy != null && enemy.IsTargetableBySpirit;
    }

    public bool TryAttackNearClick(Vector3 clickWorldPoint, Bounds islandBounds)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return false;
        if (_selection == null || _selection.Selected.Count == 0) return false;

        EnemyController snappedEnemy = FindEnemyNearClick(clickWorldPoint);
        if (snappedEnemy == null) return false;

        AttackSelected(snappedEnemy, islandBounds);
        return true;
    }

    public void MoveSelectedTo(Vector3 worldPoint)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (_selection == null || _selection.Selected.Count == 0) return;

        EnemyController snappedEnemy = FindEnemyNearClick(worldPoint);
        if (snappedEnemy != null)
        {
            AttackSelected(snappedEnemy, ResolveIslandBounds());
            return;
        }

        foreach (SpiritView spirit in _selection.Selected)
        {
            if (spirit == null) continue;
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controller.MoveTo(worldPoint);
        }

        _selection.ClearSelection();
    }

    public void InteractSelected(IInteractable target)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (_selection == null || _selection.Selected.Count == 0) return;

        foreach (SpiritView spirit in _selection.Selected)
        {
            if (spirit == null) continue;
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controller.Interact(target);
        }

        _selection.ClearSelection();
    }

    public void AttackSelected(EnemyController enemy, Bounds islandBounds)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (_selection == null || _selection.Selected.Count == 0) return;
        if (!CanAttackEnemy(enemy)) return;

        _cachedIslandBounds = islandBounds;
        _hasCachedIslandBounds = true;

        var controllers = new List<SpiritController>(_selection.Selected.Count);
        foreach (SpiritView spirit in _selection.Selected)
        {
            if (spirit == null) continue;
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controllers.Add(controller);
        }

        int squadCount = controllers.Count;
        for (int i = 0; i < squadCount; i++)
        {
            controllers[i].Attack(enemy, islandBounds, i, squadCount);
        }

        _selection.ClearSelection();
    }

    private EnemyController FindEnemyNearClick(Vector3 clickWorldPoint)
    {
        if (EnemyHoverSelector.CurrentHoveredEnemy != null &&
            EnemyHoverSelector.CurrentHoveredEnemy.IsTargetableBySpirit)
        {
            return EnemyHoverSelector.CurrentHoveredEnemy;
        }

        if (_enemyClickSnapRadius <= 0f) return null;

        EnemyController best = null;
        float bestSqrDist = _enemyClickSnapRadius * _enemyClickSnapRadius;

        for (int i = 0; i < EnemyController.ActiveEnemies.Count; i++)
        {
            EnemyController enemy = EnemyController.ActiveEnemies[i];
            if (enemy == null || !enemy.IsTargetableBySpirit) continue;

            float dx = enemy.transform.position.x - clickWorldPoint.x;
            float dz = enemy.transform.position.z - clickWorldPoint.z;
            float sqrDist = dx * dx + dz * dz;
            if (sqrDist <= bestSqrDist)
            {
                bestSqrDist = sqrDist;
                best = enemy;
            }
        }

        return best;
    }

    private Bounds ResolveIslandBounds()
    {
        if (_hasCachedIslandBounds)
            return _cachedIslandBounds;

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices != null && tri.vertices.Length > 0)
        {
            Bounds b = new Bounds(tri.vertices[0], Vector3.zero);
            for (int i = 1; i < tri.vertices.Length; i++)
                b.Encapsulate(tri.vertices[i]);
            b.Expand(new Vector3(0.5f, 4f, 0.5f));
            _cachedIslandBounds = b;
            _hasCachedIslandBounds = true;
            return b;
        }

        return new Bounds(Vector3.zero, new Vector3(100f, 20f, 100f));
    }
}