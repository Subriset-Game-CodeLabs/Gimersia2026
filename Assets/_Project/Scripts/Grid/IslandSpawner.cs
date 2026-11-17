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

    [SerializeField] private TreeNode _treePrefab;
    [SerializeField] private TreeDefinition _tunasDefinition;
    [SerializeField] private TreeDefinition _tumbangDefinition;

    [SerializeField] private TeamworkSettings _teamworkSettings;

    private void Awake()
    {
        Data = new IslandData(_definition);
        SpawnBlocks();
    }

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
            view.GetComponent<GridInteractable>()?.Initialize(block, _teamworkSettings);

            if (view.TileCollider != null) _viewsByCollider[view.TileCollider] = view;

            // Temporary: slot markers still use the old cell offsets until step 2.
            // Vector3 center = _definition.CellToWorld(block.Cell);
            // SpawnSlotMarkers(block, center, view.transform);

            SpawnTrees(view, block);
            // SpawnSlotMarkers(block, view);
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

    private void SpawnTrees(BlockView view, GridBlockData block)
    {
        var nodes = new List<TreeNode>();
        int index = 0;
        foreach (Transform slot in view.Slots)
        {
            // TODO(balance): alternates types until per-slot data exists.
            var definition = index % 2 == 0 ? _tunasDefinition : _tumbangDefinition;
            var node = Instantiate(_treePrefab, slot.position, slot.rotation, slot);
            node.Initialize(definition);
            node.GetComponent<TreeInteractable>().Initialize(_teamworkSettings);
            nodes.Add(node);
            index++;
        }

        // Added at runtime so the five block prefabs don't need editing.
        view.gameObject.AddComponent<BlockTreeRevealer>().Initialize(block, nodes);
    }
}
