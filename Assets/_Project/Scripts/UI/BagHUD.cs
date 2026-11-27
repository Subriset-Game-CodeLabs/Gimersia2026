using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Keep this on an active Canvas host outside the panel it hides.
public sealed class BagHUD : MonoBehaviour
{
    [SerializeField] private ItemInventoryProvider _inventory;
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _openButton;
    [Tooltip("Optional. The outside bag button also toggles the panel; Escape/right-click close it.")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private BagItemSlotUI[] _slots = Array.Empty<BagItemSlotUI>();
    [SerializeField] private TMP_Text _selectionText;
    [SerializeField] private TreeElementMenu _treeElementMenu;
    private ItemBagState _state;
    private PhaseManager _phases;
    private InputAction _cancel;
    private ItemUseController _itemUse;

    public bool IsOpen => _state != null && _state.IsOpen;
    public ItemInventoryProvider InventoryProvider => _inventory;
    public ItemDefinition SelectedItem => _inventory != null && _state != null
        ? _inventory.FindDefinition(_state.SelectedId) : null;
    public event Action<ItemDefinition> OnItemSelected;

    private void Awake()
    {
        _cancel = new InputAction("CancelBag", InputActionType.Button);
        _cancel.AddBinding("<Keyboard>/escape");
        _cancel.AddBinding("<Mouse>/rightButton");
    }

    private void OnEnable()
    {
        if (_openButton != null) _openButton.onClick.AddListener(Toggle);
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        if (_cancel != null)
        {
            _cancel.performed += HandleCancel;
            _cancel.Enable();
        }
        if (_state != null) _state.OnChanged += Refresh;
        BindPhases();
        Refresh();
    }

    private void Start()
    {
        _itemUse = GetComponent<ItemUseController>();
        BindPhases();
        EnsureReady();
        Refresh();
    }

    private void OnDisable()
    {
        if (_openButton != null) _openButton.onClick.RemoveListener(Toggle);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_cancel != null)
        {
            _cancel.performed -= HandleCancel;
            _cancel.Disable();
        }
        Close();
        if (_state != null) _state.OnChanged -= Refresh;
        UnbindPhases();
    }

    private void OnDestroy()
    {
        _state?.Dispose();
        _cancel?.Dispose();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (!isActiveAndEnabled || IsOpen) return;
        BindPhases();
        if (_phases == null) return;

        bool returningFromTargeting = _itemUse != null && _itemUse.IsTargeting;
        if (returningFromTargeting)
        {
            if (!_phases.GameplayPhaseAllowsInput || _phases.InputGate.HasOtherOwners(_itemUse)) return;
        }
        else if (!_phases.GameplayInputAllowed) return;

        if (!EnsureReady()) return;
        if (_treeElementMenu != null) _treeElementMenu.Close();
        _phases.InputGate.Acquire(this);
        if (returningFromTargeting) _itemUse.Cancel();
        _state.Open();
    }

    public void Close()
    {
        _state?.Close();
        if (_phases != null) _phases.InputGate.Release(this, Time.frameCount);
        Refresh();
    }

    private void Choose(string id)
    {
        if (!isActiveAndEnabled || _phases == null || !_phases.GameplayPhaseAllowsInput) return;
        ItemDefinition definition = _inventory.FindDefinition(id);
        if (definition == null || !definition.TryValidate(out _) || !_state.TrySelect(id)) return;
        // The targeting controller takes ownership before closing this panel.
        OnItemSelected?.Invoke(definition);
    }

    private void HandleCancel(InputAction.CallbackContext context)
    {
        if (IsOpen) Close();
    }
    private void HandlePhase(int number) => Refresh();
    private void HandleReward(int number) => Close();

    private void BindPhases()
    {
        PhaseManager phases = PhaseManager.Instance;
        if (_phases == phases) return;
        Close();
        UnbindPhases();
        _phases = phases;
        if (_phases == null) return;
        _phases.OnDayStart += HandlePhase;
        _phases.OnNightStart += HandlePhase;
        _phases.OnRewardStart += HandleReward;
        _phases.OnRunFinished += Close;
    }

    private void UnbindPhases()
    {
        if (_phases == null) return;
        _phases.OnDayStart -= HandlePhase;
        _phases.OnNightStart -= HandlePhase;
        _phases.OnRewardStart -= HandleReward;
        _phases.OnRunFinished -= Close;
        _phases = null;
    }

    private bool EnsureReady()
    {
        if (!TryValidateConfiguration(out string error))
        {
            Debug.LogError("Bag HUD: " + error, this);
            return false;
        }

        if (_state == null)
        {
            _state = new ItemBagState(_inventory.Inventory);
            if (isActiveAndEnabled) _state.OnChanged += Refresh;
        }

        return true;
    }

    private bool TryValidateConfiguration(out string error)
    {
        error = null;
        if (_inventory == null || !_inventory.EnsureInitialized())
        {
            error = "Assign a valid ItemInventoryProvider.";
            return false;
        }

        return TryValidateControls(out error) && TryValidateSlots(out error);
    }

    private bool TryValidateControls(out string error)
    {
        error = null;
        if (!HasSeparatePanel() || _openButton == null || _openButton.transform.IsChildOf(_panel.transform))
        {
            error = "Use a separate panel and an outside bag button.";
            return false;
        }

        if (_closeButton != null && (_openButton == _closeButton || !_closeButton.transform.IsChildOf(_panel.transform)))
        {
            error = "If assigned, the close button must be distinct and inside the panel.";
            return false;
        }

        return true;
    }

    private bool TryValidateSlots(out string error)
    {
        error = null;
        if (_slots == null || _slots.Length != _inventory.Catalog.Count)
        {
            error = "Assign one slot per catalog item in matching order.";
            return false;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            BagItemSlotUI slot = _slots[i];
            if (!HasValidSlotLayout(slot))
            {
                error = $"Configure slot {i}, its button and text fields inside the panel.";
                return false;
            }

            for (int j = 0; j < i; j++)
            {
                if (slot == _slots[j] || slot.Button == _slots[j].Button)
                {
                    error = "Each slot needs a distinct component and button.";
                    return false;
                }
            }
        }

        return true;
    }

    private bool HasValidSlotLayout(BagItemSlotUI slot)
    {
        return slot != null && slot.enabled && slot.gameObject.activeSelf && slot.IsConfigured
            && slot.transform.IsChildOf(_panel.transform) && slot.Button.transform.IsChildOf(slot.transform)
            && slot.Button != _closeButton && slot.Button != _openButton;
    }

    private bool HasSeparatePanel() => _panel != null && _panel != gameObject && !transform.IsChildOf(_panel.transform);

    private void Refresh()
    {
        bool show = isActiveAndEnabled && IsOpen;
        if (_openButton != null) _openButton.interactable = isActiveAndEnabled && _phases != null
            && (_phases.CurrentPhase == PhaseType.Day || _phases.CurrentPhase == PhaseType.Night);
        if (!show)
        {
            HidePanel();
            return;
        }

        ShowPanel();
    }

    private void ShowPanel()
    {
        _panel.SetActive(true);
        for (int i = 0; i < _slots.Length; i++)
        {
            ItemDefinition item = _inventory.Catalog[i];
            _slots[i].Bind(item, _inventory.Inventory.GetQuantity(item.Id), _state.SelectedId == item.Id, Choose);
        }

        if (_selectionText == null) return;

        ItemDefinition selected = SelectedItem;
        _selectionText.text = selected != null
            ? selected.Title + " selected."
            : "Choose an item. Right-click or Escape to close.";
    }

    private void HidePanel()
    {
        ClearPanelSelection();
        ClearSlots();
        if (HasSeparatePanel()) _panel.SetActive(false);
    }

    private void ClearPanelSelection()
    {
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected == null || _panel == null) return;
        if (selected.transform.IsChildOf(_panel.transform)) events.SetSelectedGameObject(null);
    }

    private void ClearSlots()
    {
        if (_slots == null) return;

        foreach (BagItemSlotUI slot in _slots)
        {
            if (slot != null) slot.Clear();
        }
    }
}
