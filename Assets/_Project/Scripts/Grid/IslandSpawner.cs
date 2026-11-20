using System.Collections.Generic;
using UnityEngine;

public class IslandSpawner : MonoBehaviour
{
    [SerializeField] private IslandDefinition _definition;
    [SerializeField] private Transform _blocksParent;

    private readonly Dictionary<Collider, BlockView> _viewsByCollider = new Dictionary<Collider, BlockView>();
    private readonly Dictionary<Collider, IslandTile> _tilesByCollider = new Dictionary<Collider, IslandTile>();
    private readonly List<BlockView> _views = new List<BlockView>();
    public IslandData Data { get; private set; }

    [SerializeField] private TreeNode _treePrefab;


    [SerializeField] private TeamworkSettings _teamworkSettings;

    private void Awake()
    {
        if (_definition == null)
        {
            Debug.LogError("IslandSpawner needs this level's IslandDefinition.", this);
            return;
        }
        var ids = new HashSet<string>();
        foreach (GridBlockDefinition block in _definition.Blocks)
        {
            if (block == null || string.IsNullOrWhiteSpace(block.Id) || !ids.Add(block.Id))
            {
                Debug.LogError("IslandDefinition needs non-empty, unique block IDs.", _definition);
                return;
            }
        }
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

            foreach (Collider collider in view.TileColliders)
                if (collider != null) _viewsByCollider[collider] = view;

            foreach (IslandTile tile in view.Tiles)
                if (tile.ClickCollider != null) _tilesByCollider[tile.ClickCollider] = tile;
            SpawnTrees(view, block);
        }
    }

    public IslandTile GetTileFromCollider(Collider collider)
    {
        return collider != null && _tilesByCollider.TryGetValue(collider, out var tile) ? tile : null;
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
            if (!view.TryGetBounds(out Bounds blockBounds)) continue;
            if (!found) { bounds = blockBounds; found = true; }
            else bounds.Encapsulate(blockBounds);
        }
        return found;
    }

    private void SpawnTrees(BlockView view, GridBlockData block)
    {
        var nodes = new List<TreeNode>();
        foreach (IslandTile tile in view.Tiles)
        {
            if (tile.TreeDefinition == null) continue;
            if (_treePrefab == null || tile.TreePoint == null || tile.InteractionPoint == null || TreeRegistry.Instance == null)
            {
                Debug.LogError("Cannot spawn tree: assign Tree Prefab, TreeRegistry, Tree Point and Interaction Point.", tile);
                continue;
            }
            Transform point = tile.TreePoint;
            var node = Instantiate(_treePrefab, point.position, point.rotation, point);
            node.SetInteractionPoint(tile.InteractionPoint);
            node.Initialize(tile.TreeDefinition);
            TreeInteractable growth = node.GetComponent<TreeInteractable>();
            if (growth != null) growth.Initialize(_teamworkSettings);
            else Debug.LogError("Tree Prefab needs TreeInteractable.", node);
            TreeElementInteractable element = node.GetComponent<TreeElementInteractable>();
            if (element == null) element = node.gameObject.AddComponent<TreeElementInteractable>();
            element.Initialize(_teamworkSettings);
            TreeRegistry.Instance.Register(node);
            tile.SetTree(node);
            nodes.Add(node);
        }

        view.gameObject.AddComponent<BlockTreeRevealer>().Initialize(block, nodes);
    }
}
