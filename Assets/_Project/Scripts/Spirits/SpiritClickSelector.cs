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
    private bool _clickPending;
    private Vector2 _pendingScreenPosition;

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void OnEnable()
    {
        if (_clickAction != null && _clickAction.action != null)
        {
            _clickAction.action.Enable();
            _clickAction.action.performed += OnClick;
        }
    }

    private void OnDisable()
    {
        if (_clickAction != null && _clickAction.action != null)
        {
            _clickAction.action.performed -= OnClick;
            _clickAction.action.Disable();
        }
        _clickPending = false;
    }

    private void OnClick(InputAction.CallbackContext context)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        _pendingScreenPosition = mouse.position.ReadValue();
        _clickPending = true;
    }

    private void Update()
    {
        if (!_clickPending) return;
        _clickPending = false;

        ProcessClick(_pendingScreenPosition);
    }

    private void ProcessClick(Vector2 screenPosition)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        if (_treeElementMenu != null) _treeElementMenu.Close();
        Ray ray = _camera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance,
            _clickLayers & Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return;

        // 1. Klik Spirit:
        //    - Tanpa Shift: klik sekali = pilih, klik ulang Spirit yang sama = batal pilih
        //    - Dengan Left Shift: tambah/kurangi hingga maksimal 3 Spirit
        SpiritView spirit = hit.collider.GetComponentInParent<SpiritView>();
        if (spirit != null)
        {
            bool isLeftShiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            if (isLeftShiftHeld)
                _selection.ToggleMulti(spirit);
            else
                _selection.ToggleSingle(spirit);
            return;
        }

        bool hasSelectedSpirit = _selection != null && _selection.Selected.Count > 0;

        // 2. Klik Musuh -> Butuh Spirit terpilih (otomatis unselect setelah diberi perintah)
        EnemyController enemy = hit.collider.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            if (hasSelectedSpirit && enemy.IsAlive && _island.TryGetBounds(out Bounds islandBounds))
                _commander.AttackSelected(enemy, islandBounds);
            return;
        }

        // 3. Klik Blok Kotor (Corrupted) -> Butuh Spirit terpilih
        BlockView block = _island.GetViewFromCollider(hit.collider);
        if (block != null && block.Data.State == GridState.Corrupted)
        {
            if (!hasSelectedSpirit) return;

            GridInteractable cleaning = block.GetComponent<GridInteractable>();
            if (cleaning != null && cleaning.CanInteract)
            {
                block.TriggerClickFeedback(_selection.Selected);
                _commander.InteractSelected(cleaning);
            }
            return;
        }

        // 4. Klik Petak Bersih / Pohon
        IslandTile tile = _island.GetTileFromCollider(hit.collider);
        TreeNode tree = hit.collider.GetComponentInParent<TreeNode>();
        if (tree == null && tile != null && tile.IsSelectable) tree = tile.Tree;
        if (tree != null && tree.IsTargetable)
        {
            if (tree.Data.IsFullyGrown)
            {
                if (_treeElementMenu != null) _treeElementMenu.Open(tree, screenPosition);
                return;
            }

            if (!hasSelectedSpirit) return;

            tree.PlayClickFeedback();
            if (tile != null) tile.TriggerClickFeedback(_selection.Selected);

            if (tree.Data.NeedsHealing)
            {
                TreeHealInteractable healWork = tree.GetComponent<TreeHealInteractable>();
                if (healWork != null && healWork.CanInteract)
                    _commander.InteractSelected(healWork);
            }
            else
            {
                TreeInteractable growthWork = tree.GetComponent<TreeInteractable>();
                if (growthWork != null)
                {
                    growthWork.PrepareForNewCommand();
                    if (growthWork.CanInteract)
                        _commander.InteractSelected(growthWork);
                }
            }
            return;
        }

        // 5. Klik Petak Tujuan Gerak -> Butuh Spirit terpilih
        if (tile != null && tile.CanMoveTo)
        {
            if (!hasSelectedSpirit) return;

            tile.TriggerClickFeedback(_selection.Selected);
            _commander.MoveSelectedTo(tile.MovementPoint);
        }
    }
}