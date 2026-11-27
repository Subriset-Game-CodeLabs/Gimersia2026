using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class EnemyHoverSelector : MonoBehaviour
{
    public static EnemyController CurrentHoveredEnemy { get; private set; }
    public static event Action<EnemyController> OnHoveredEnemyChanged;

    [SerializeField] private SpiritSelection _selection;
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _enemyLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float _hoverAssistRadius = 0.45f;

    private EnemyController _hoveredEnemy;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        if (_selection == null) _selection = FindFirstObjectByType<SpiritSelection>();
    }

    private void Update()
    {
        bool hasSelectedSpirit = _selection != null && _selection.Selected.Count > 0;
        Mouse mouse = Mouse.current;

        if (!hasSelectedSpirit || mouse == null || _camera == null ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            SetHoveredEnemy(null);
            return;
        }

        Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
        EnemyController hitEnemy = null;
        int mask = _enemyLayers & Physics.DefaultRaycastLayers;

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, mask, QueryTriggerInteraction.Collide))
        {
            EnemyController candidate = hit.collider.GetComponentInParent<EnemyController>();
            if (candidate != null && candidate.IsTargetableBySpirit)
            {
                hitEnemy = candidate;
            }
        }

        if (hitEnemy == null && _hoverAssistRadius > 0f &&
            Physics.SphereCast(ray, _hoverAssistRadius, out RaycastHit assistHit, 500f, mask, QueryTriggerInteraction.Collide))
        {
            EnemyController candidate = assistHit.collider.GetComponentInParent<EnemyController>();
            if (candidate != null && candidate.IsTargetableBySpirit)
            {
                hitEnemy = candidate;
            }
        }

        SetHoveredEnemy(hitEnemy);
    }

    private void SetHoveredEnemy(EnemyController enemy)
    {
        if (_hoveredEnemy == enemy) return;
        if (_hoveredEnemy != null) _hoveredEnemy.SetHovered(false);

        _hoveredEnemy = enemy;
        CurrentHoveredEnemy = enemy;

        if (_hoveredEnemy != null)
        {
            _hoveredEnemy.SetHovered(true);
            AudioManager.Instance.PlayEnemyHover(_hoveredEnemy.transform.position);
        }
        OnHoveredEnemyChanged?.Invoke(_hoveredEnemy);
    }

    private void OnDisable()
    {
        SetHoveredEnemy(null);
    }
}