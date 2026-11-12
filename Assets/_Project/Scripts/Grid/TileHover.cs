using UnityEngine;
using UnityEngine.InputSystem;

public class TileHover : MonoBehaviour
{
    [SerializeField] private IslandSpawner _island;
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _tileLayers = ~0;

    private BlockView _hovered;

    public BlockView Hovered => _hovered;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
    }

    private void Update()
    {
        // Mouse.current is null if no mouse is connected.
        Mouse mouse = Mouse.current;
        if (mouse == null) { SetHovered(null); return; }

        Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
        BlockView hit = null;

        if (Physics.Raycast(ray, out RaycastHit info, 500f, _tileLayers))
            hit = _island.GetViewFromCollider(info.collider);

        SetHovered(hit);
    }

    private void SetHovered(BlockView view)
    {
        if (view == _hovered) return;
        if (_hovered != null) _hovered.SetHighlighted(false);
        _hovered = view;
        if (_hovered != null) _hovered.SetHighlighted(true);
    }

    private void OnDisable()
    {
        SetHovered(null);
    }
}
