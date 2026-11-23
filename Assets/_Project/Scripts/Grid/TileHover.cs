using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TileHover : MonoBehaviour
{
    [SerializeField] private IslandSpawner _island;
    [SerializeField] private SpiritSelection _selection;
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _tileLayers = ~0;

    private BlockView _hovered;
    private IslandTile _hoveredTile;

    public BlockView Hovered => _hovered;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        if (_selection == null) _selection = FindFirstObjectByType<SpiritSelection>();
    }

    private void Update()
    {
        bool hasSelectedSpirit = _selection != null && _selection.Selected.Count > 0;
        Mouse mouse = Mouse.current;

        // Jika pemain belum mengklik/memilih Spirit sama sekali, matikan seluruh efek hover grid
        if (!hasSelectedSpirit || mouse == null || _camera == null || _island == null ||
            (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            SetHovered(null);
            SetHoveredTile(null);
            return;
        }

        Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
        BlockView hit = null;
        IslandTile tile = null;

        if (Physics.Raycast(ray, out RaycastHit info, 500f,
            _tileLayers & Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            hit = _island.GetViewFromCollider(info.collider);
            if (hit != null && hit.Data.State != GridState.Corrupted) hit = null;
            tile = _island.GetTileFromCollider(info.collider);
            if (tile != null && !tile.IsSelectable) tile = null;
        }

        SetHovered(hit);
        SetHoveredTile(tile);
    }

    private void SetHovered(BlockView view)
    {
        if (view == _hovered) return;
        if (_hovered != null) _hovered.SetHighlighted(false);
        _hovered = view;
        if (_hovered != null) _hovered.SetHighlighted(true);
    }

    private void SetHoveredTile(IslandTile tile)
    {
        if (tile == _hoveredTile) return;
        if (_hoveredTile != null) _hoveredTile.SetHighlighted(false);
        _hoveredTile = tile;
        if (_hoveredTile != null) _hoveredTile.SetHighlighted(true);
    }

    private void OnDisable()
    {
        SetHovered(null);
        SetHoveredTile(null);
    }
}