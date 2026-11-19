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
        _treeElementMenu.Close();
        Ray ray = _camera.ScreenPointToRay(screenPosition);

        // Collide: the spirit's click collider is a trigger.
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, ~0, QueryTriggerInteraction.Collide)) return;
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

        TreeNode tree = hit.collider.GetComponentInParent<TreeNode>();
        if (tree != null && tree.IsTargetable)
        {
            if (tree.Data.IsFullyGrown)
            {
                _treeElementMenu.Open(tree, screenPosition);
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

        // Interactables first: grids and (later) trees. Done targets fall through to a plain move.
        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable != null && interactable.CanInteract)
        {
            _commander.InteractSelected(interactable);
            return;
        }

        // Only tiles are valid move targets.
        if (_island.GetViewFromCollider(hit.collider) != null)
        {
            _commander.MoveSelectedTo(hit.point);
        }
    }

}
