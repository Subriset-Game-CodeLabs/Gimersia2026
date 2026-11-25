using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BagHUD))]
public sealed class ItemUseController : MonoBehaviour
{
    [SerializeField] private ItemTargetResolver _targetResolver;
    [SerializeField] private ItemTargetPreview _preview;
    [Tooltip("Visual marker size for a single tree; not an effect radius.")]
    [SerializeField, Min(0.01f)] private float _singleTreePreviewRadius = 0.6f;
    private BagHUD _bag;
    private PhaseManager _phases;
    private ItemUseSession _session;
    private InputAction _confirm;
    private InputAction _cancel;
    private bool _confirmPending;
    private bool _usingTeam;
    private bool _teamCancelled;
    private Vector2 _pendingPosition;

    public bool IsTargeting => _session != null && _session.IsTargeting;

    private bool IsConfigured => _bag != null && _bag.InventoryProvider != null
        && _targetResolver != null && _targetResolver.IsConfigured
        && _preview != null && _preview.IsConfigured;

    private void Awake()
    {
        _bag = GetComponent<BagHUD>();
        _confirm = new InputAction("ConfirmItem", InputActionType.Button);
        _confirm.AddBinding("<Mouse>/leftButton");
        _cancel = new InputAction("CancelItem", InputActionType.Button);
        _cancel.AddBinding("<Keyboard>/escape");
        _cancel.AddBinding("<Mouse>/rightButton");
    }

    private void OnEnable()
    {
        if (_bag != null) _bag.OnItemSelected += HandleItemSelected;
        _confirm.performed += QueueConfirmation;
        _cancel.performed += HandleCancel;
        _confirm.Enable();
        _cancel.Enable();
        BindPhases();
    }

    private void Start()
    {
        BindPhases();
        if (!IsConfigured)
            Debug.LogError("Item use: assign a configured Target Resolver and Preview on an active host with BagHUD and its inventory.", this);
    }

    private void OnDisable()
    {
        if (_bag != null) _bag.OnItemSelected -= HandleItemSelected;
        _confirm.performed -= QueueConfirmation;
        _cancel.performed -= HandleCancel;
        _confirm.Disable();
        _cancel.Disable();
        Cancel();
        UnbindPhases();
    }

    private void OnDestroy()
    {
        _confirm.Dispose();
        _cancel.Dispose();
    }

    public bool TryBegin(ItemDefinition item)
    {
        if (_usingTeam || _bag == null || _bag.InventoryProvider == null || !CanUseItems()) return false;
        bool team = item != null && item.TargetsTeam;
        if (!team && !IsConfigured) return false;
        ItemInventoryProvider provider = _bag.InventoryProvider;
        if (!provider.EnsureInitialized()) return false;
        if (_session == null) _session = new ItemUseSession(provider.Inventory, CanUseItems);
        if (team)
        {
            _usingTeam = true;
            _teamCancelled = false;
            try
            {
                bool used = _session.TryUseTeam(item);
                if (used)
                {
                    _bag.Close();
                    Cancel();
                }
                return used;
            }
            finally
            {
                _usingTeam = false;
            }
        }
        if (!_session.TryBegin(item, Time.frameCount)) return false;

        // Acquire before closing the bag so ownership never drops between modes.
        _phases.InputGate.Acquire(this);
        _bag.Close();
        _confirmPending = false;
        UpdatePreview();
        return true;
    }

    public void Cancel()
    {
        if (_usingTeam) _teamCancelled = true;
        _session?.Cancel();
        _confirmPending = false;
        if (_preview != null) _preview.Hide();
        if (_phases != null) _phases.InputGate.Release(this, Time.frameCount);
    }

    private bool CanUseItems()
    {
        if (_usingTeam && _teamCancelled) return false;
        if (!isActiveAndEnabled || _bag == null || !_bag.isActiveAndEnabled) return false;
        if (_phases == null || !_phases.GameplayPhaseAllowsInput) return false;

        object owner = _bag.IsOpen ? (object)_bag : this;
        return !_phases.InputGate.HasOtherOwners(owner);
    }

    private void HandleItemSelected(ItemDefinition item) => TryBegin(item);
    private void HandleCancel(InputAction.CallbackContext context) => Cancel();
    private void HandleReward(int number) => Cancel();

    private void QueueConfirmation(InputAction.CallbackContext context)
    {
        if (!IsTargeting || Mouse.current == null) return;
        _pendingPosition = Mouse.current.position.ReadValue();
        _confirmPending = true;
    }

    private void LateUpdate()
    {
        if (!IsTargeting) return;
        if (!CanUseItems() || !_session.HasStock || !_targetResolver.isActiveAndEnabled || !_preview.isActiveAndEnabled)
        {
            Cancel();
            return;
        }

        // Resolve after the EventSystem so a UI click never becomes a world use.
        if (_confirmPending)
        {
            _confirmPending = false;
            ConfirmAt(_pendingPosition);
        }
        if (IsTargeting) UpdatePreview();
    }

    private void ConfirmAt(Vector2 position)
    {
        if (IsPointerOverUI()) return;

        bool used;
        if (_session.SelectedItem.Effect == ItemEffectType.InstantGrowth)
        {
            if (!_targetResolver.TryResolveTree(position, out TreeInteractable tree)) return;
            used = _session.TryUseTree(tree, Time.frameCount);
        }
        else
        {
            if (!_targetResolver.TryResolve(position, out Vector3 point)) return;
            used = _session.TryUse(point, Time.frameCount);
        }
        if (used) Cancel();
    }

    private void UpdatePreview()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || IsPointerOverUI())
        {
            _preview.Hide();
            return;
        }
        Vector2 position = mouse.position.ReadValue();
        if (_session.SelectedItem.Effect == ItemEffectType.InstantGrowth)
        {
            UpdateTreePreview(position);
            return;
        }
        if (!_targetResolver.TryResolve(position, out Vector3 point))
        {
            _preview.Hide();
            return;
        }
        _preview.Show(point, _session.SelectedItem.Radius, _session.HasTargets(point));
    }

    private void UpdateTreePreview(Vector2 position)
    {
        if (_targetResolver.TryResolveTree(position, out TreeInteractable tree))
        {
            _preview.Show(tree.transform.position, _singleTreePreviewRadius, _session.HasTreeTarget(tree));
        }
        else if (_targetResolver.TryResolve(position, out Vector3 point))
        {
            _preview.Show(point, _singleTreePreviewRadius, false);
        }
        else
        {
            _preview.Hide();
        }
    }

    private static bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void BindPhases()
    {
        PhaseManager phases = PhaseManager.Instance;
        if (_phases == phases) return;
        Cancel();
        UnbindPhases();
        _phases = phases;
        if (_phases == null) return;
        _phases.OnRewardStart += HandleReward;
        _phases.OnRunFinished += Cancel;
    }

    private void UnbindPhases()
    {
        if (_phases == null) return;
        _phases.OnRewardStart -= HandleReward;
        _phases.OnRunFinished -= Cancel;
        _phases = null;
    }
}
