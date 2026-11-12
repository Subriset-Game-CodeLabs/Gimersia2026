using System.Collections.Generic;
using UnityEngine;

public class IslandSpawner : MonoBehaviour
{
    [SerializeField] private IslandDefinition _definition;
    [SerializeField] private BlockView _blockPrefab;
    [SerializeField] private GameObject _slotMarkerPrefab;
    [SerializeField] private Transform _blocksParent;

    private readonly Dictionary<Collider, BlockView> _viewsByCollider = new Dictionary<Collider, BlockView>();

    public IslandData Data { get; private set; }

    private void Awake()
    {
        Data = new IslandData(_definition);
        SpawnBlocks();
    }

    private void SpawnBlocks()
    {
        foreach (var block in Data.Blocks)
        {
            Vector3 center = _definition.CellToWorld(block.Cell);
            BlockView view = Instantiate(_blockPrefab, center, Quaternion.identity, _blocksParent);
            view.name = $"Block_{block.Id}";

            // Scale the tile to match the authored block size.
            view.transform.localScale = new Vector3(_definition.BlockSize, 1f, _definition.BlockSize);

            view.Initialize(block);
            
            if (view.TileCollider != null) _viewsByCollider[view.TileCollider] = view;

            SpawnSlotMarkers(block, center, view.transform);
        }
    }

    private void SpawnSlotMarkers(GridBlockData block, Vector3 center, Transform parent)
    {
        if (_slotMarkerPrefab == null) return;

        foreach (var offset in block.TreeSlotOffsets)
        {
            Vector3 pos = center + new Vector3(offset.x, 0.5f, offset.y);
            GameObject marker = Instantiate(_slotMarkerPrefab, pos, Quaternion.identity);
            // Parent without inheriting the tile's non-uniform scale.
            marker.transform.SetParent(_blocksParent, true);
            marker.name = $"Slot_{block.Id}";
        }
    }

    /// <summary>Finds the block view for a collider hit by a raycast. Returns null if none.</summary>
    public BlockView GetViewFromCollider(Collider collider)
    {
        return collider != null && _viewsByCollider.TryGetValue(collider, out var view) ? view : null;
    }

}
