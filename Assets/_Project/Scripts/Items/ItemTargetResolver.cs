using UnityEngine;

public sealed class ItemTargetResolver : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [Tooltip("Terrain surfaces only. Exclude spirits, enemies and tree click hitboxes.")]
    [SerializeField] private LayerMask _groundLayers;
    [Tooltip("Single-tree picking. Include tree hitboxes and foreground blockers; separate from terrain sampling.")]
    [SerializeField] private LayerMask _treeLayers = ~0;
    [Tooltip("Optional water renderer. Its height and XZ bounds support water without a collider.")]
    [SerializeField] private Renderer _waterSurface;
    [SerializeField, Min(1f)] private float _maxDistance = 500f;

    public bool IsConfigured => _camera != null && _groundLayers.value != 0 && _maxDistance > 0f;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    public bool TryResolve(Vector2 screenPosition, out Vector3 point)
    {
        point = default;
        if (!IsConfigured) return false;

        Ray ray = _camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _groundLayers, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        return TryResolveWater(ray, out point);
    }

    public bool TryResolveTree(Vector2 screenPosition, out TreeInteractable tree)
    {
        tree = null;
        if (!IsConfigured || _treeLayers.value == 0) return false;

        Ray ray = _camera.ScreenPointToRay(screenPosition);
        // Use the nearest collider, including trigger hitboxes; never pick through a blocker.
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _treeLayers, QueryTriggerInteraction.Collide)) return false;
        if (hit.collider == null) return false;

        tree = hit.collider.GetComponentInParent<TreeInteractable>();
        return tree != null;
    }

    private bool TryResolveWater(Ray ray, out Vector3 point)
    {
        point = default;
        if (_waterSurface == null || !_waterSurface.enabled || !_waterSurface.gameObject.activeInHierarchy) return false;

        var plane = new Plane(Vector3.up, _waterSurface.transform.position);
        if (!plane.Raycast(ray, out float distance) || distance > _maxDistance) return false;

        Vector3 candidate = ray.GetPoint(distance);
        Bounds bounds = _waterSurface.bounds;
        bool inside = candidate.x >= bounds.min.x && candidate.x <= bounds.max.x
            && candidate.z >= bounds.min.z && candidate.z <= bounds.max.z;
        if (!inside) return false;

        point = candidate;
        return true;
    }
}
