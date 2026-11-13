using System.Collections.Generic;
using UnityEngine;

public class IslandSpawner : MonoBehaviour
{
    [SerializeField] private IslandDefinition _definition;
    //[SerializeField] private BlockView _blockPrefab;
    [SerializeField] private GameObject _slotMarkerPrefab;
    [SerializeField] private Transform _blocksParent;

    private readonly Dictionary<Collider, BlockView> _viewsByCollider = new Dictionary<Collider, BlockView>();
    private readonly List<BlockView> _views = new List<BlockView>();
    public IslandData Data { get; private set; }

    private void Awake()
    {
        Data = new IslandData(_definition);
        SpawnBlocks();
    }

    // private void SpawnBlocks()
    // {
    //     foreach (var block in Data.Blocks)
    //     {
    //         Vector3 center = _definition.CellToWorld(block.Cell);
    //         BlockView view = Instantiate(_blockPrefab, center, Quaternion.identity, _blocksParent);
    //         view.name = $"Block_{block.Id}";

    //         // Scale the tile to match the authored block size.
    //         view.transform.localScale = new Vector3(_definition.BlockSize, 1f, _definition.BlockSize);

    //         view.Initialize(block);

    //         view.GetComponent<GridInteractable>()?.Initialize(block);

    //         if (view.TileCollider != null) _viewsByCollider[view.TileCollider] = view;

    //         SpawnSlotMarkers(block, center, view.transform);
    //     }
    // }

    private void SpawnBlocks()
    {
        foreach (var blockDef in _definition.Blocks)
        {
            if (blockDef.ViewPrefab == null)
            {
                Debug.LogError($"Block '{blockDef.Id}' has no View Prefab in IslandDefinition.", _definition);
                continue;
            }

            GridBlockData block = Data.GetBlock(blockDef.Id);

            // The prefab is already shaped and positioned, so spawn at the origin, unscaled.
            BlockView view = Instantiate(blockDef.ViewPrefab, Vector3.zero, Quaternion.identity, _blocksParent);
            view.name = $"Block_{block.Id}";
            view.Initialize(block);
            _views.Add(view);
            //initialize grid interactable 
            view.GetComponent<GridInteractable>()?.Initialize(block);

            if (view.TileCollider != null) _viewsByCollider[view.TileCollider] = view;

            // Temporary: slot markers still use the old cell offsets until step 2.
            // Vector3 center = _definition.CellToWorld(block.Cell);
            // SpawnSlotMarkers(block, center, view.transform);

            SpawnSlotMarkers(block, view);
        }
    }

    private void SpawnSlotMarkers(GridBlockData block, BlockView view)
    {
        if (_slotMarkerPrefab == null) return;

        foreach (Transform slot in view.Slots)
        {
            // Placeholder only. The tree task will spawn trees at these same slots.
            // Parented to the slot: the block root is unscaled now, so nothing stretches.
            Vector3 pos = slot.position + Vector3.up * 0.1f; // half the cylinder height
            GameObject marker = Instantiate(_slotMarkerPrefab, pos, Quaternion.identity, slot);
            marker.name = $"SlotMarker_{block.Id}";
        }
    }

    /// <summary>Finds the block view for a collider hit by a raycast. Returns null if none.</summary>
    public BlockView GetViewFromCollider(Collider collider)
    {
        return collider != null && _viewsByCollider.TryGetValue(collider, out var view) ? view : null;
    }

    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (var view in _views)
        {
            Renderer tile = view.TileRenderer;
            if (tile == null) continue;

            if (!found) { bounds = tile.bounds; found = true; }
            else bounds.Encapsulate(tile.bounds);
        }
        return found;
    }
}
