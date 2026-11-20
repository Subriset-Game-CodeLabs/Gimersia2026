using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SpiritClickSelector : MonoBehaviour
{
    [SerializeField] private InputActionReference _clickAction;
    [SerializeField] private SpiritSelection _selection;
    [SerializeField] private SpiritCommander _commander;
    [SerializeField] private IslandSpawner _island;
    [SerializeField] private TreeElementMenu _treeElementMenu;
    [SerializeField] private float _maxDistance = 200f;
    [SerializeField] private LayerMask _clickLayers = Physics.DefaultRaycastLayers;

    private Camera _camera;

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void OnEnable()
    {
        _clickAction.action.Enable();
        _clickAction.action.performed += OnClick;
    }

    private void OnDisable()
    {
        _clickAction.action.performed -= OnClick;
        _clickAction.action.Disable();
    }

    private void OnClick(InputAction.CallbackContext context)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 screenPosition = mouse.position.ReadValue();
        if (_treeElementMenu != null) _treeElementMenu.Close();
        Ray ray = _camera.ScreenPointToRay(screenPosition);

        // Collide: the spirit's click collider is a trigger.
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance,
            _clickLayers & Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return;
        SpiritView spirit = hit.collider.GetComponentInParent<SpiritView>();
        if (spirit != null)
        {
            _selection.SelectOnly(spirit);
            return;
        }
        EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            if (enemy.IsAlive && _island.TryGetBounds(out Bounds islandBounds))
                _commander.AttackSelected(enemy, islandBounds);
            return;
        }

        // Dirty blocks consume the entire click and always share one cleaning target.
        BlockView block = _island.GetViewFromCollider(hit.collider);
        if (block != null && block.Data.State == GridState.Corrupted)
        {
            GridInteractable cleaning = block.GetComponent<GridInteractable>();
            if (cleaning != null && cleaning.CanInteract) _commander.InteractSelected(cleaning);
            return;
        }

        IslandTile tile = _island.GetTileFromCollider(hit.collider);
        TreeNode tree = hit.collider.GetComponentInParent<TreeNode>();
        if (tree == null && tile != null && tile.IsSelectable) tree = tile.Tree;
        if (tree != null && tree.IsTargetable)
        {
            if (tree.Data.IsFullyGrown)
            {
                if (_treeElementMenu != null) _treeElementMenu.Open(tree, screenPosition);
            }
            else
            {
                // Repair a damaged sapling before continuing its growth.
                IInteractable work = tree.Data.NeedsHealing
                    ? (IInteractable)tree.GetComponent<TreeHealInteractable>()
                    : tree.GetComponent<TreeInteractable>();
                if (work != null && work.CanInteract)
                    _commander.InteractSelected(work);
            }
            return;
        }

        // Never fall back to arbitrary ground hit positions or reserved tree tiles.
        if (tile != null && tile.CanMoveTo)
        {
            _commander.MoveSelectedTo(tile.MovementPoint);
        }
    }

}
