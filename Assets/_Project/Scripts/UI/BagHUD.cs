using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Keep this on an active Canvas host outside the panel it hides.
public sealed class BagHUD : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private ItemInventoryProvider _inventory;
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _openButton;
    [Tooltip("Optional. The outside bag button also toggles the panel; Escape/right-click close it.")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private BagItemSlotUI[] _slots = Array.Empty<BagItemSlotUI>();
    [SerializeField] private TMP_Text _selectionText;
    [SerializeField] private TreeElementMenu _treeElementMenu;

    [Header("UI v2 Backpack Button & Count Badge")]
    [SerializeField] private Image _bagButtonImage;
    [SerializeField] private Sprite _bagClosedSprite;
    [SerializeField] private Sprite _bagOpenSprite;
    [SerializeField] private Sprite _bagAimingSprite;
    [SerializeField] private Image _bagButtonIcon;
    [SerializeField] private Sprite _defaultBackpackIcon;
    [SerializeField] private RectTransform _bagButtonContent;
    [SerializeField] private Vector2 _bagOpenContentOffset = new Vector2(0f, 4.5f);
    [SerializeField] private GameObject _totalCountBadge;
    [SerializeField] private TMP_Text _totalCountText;

    [Header("UI v2 Count-Change Pop (-1 / +1)")]
    [SerializeField] private RectTransform _countChangePopRect;
    [SerializeField] private TMP_Text _countChangePopText;
    [SerializeField] private CanvasGroup _countChangePopGroup;

    private static readonly Color MinusColor = new Color(1f, 0.52f, 0.42f, 1f);
    private static readonly Color PlusColor = new Color(0.58f, 0.94f, 0.44f, 1f);

    private ItemBagState _state;
    private PhaseManager _phases;
    private InputAction _cancel;
    private InputAction _toggleKey;
    private ItemUseController _itemUse;
    private ItemInventory _subscribedInventory;
    private ItemDefinition _aimingItem;
    private Tween _panelTween;
    private Tween _popTween;
    private Tween _buttonPunchTween;
    private Tween _bagContentTween;
    private Vector2 _bagContentBasePos;
    private bool _bagContentBaseCaptured;
    private Vector2 _countPopBasePos;
    private bool _countPopBaseCaptured;
    private bool _wasOpen;

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

        _toggleKey = new InputAction("ToggleBag", InputActionType.Button);
        _toggleKey.AddBinding("<Keyboard>/q");

        if (_bagButtonContent != null)
        {
            _bagContentBasePos = _bagButtonContent.anchoredPosition;
            _bagContentBaseCaptured = true;
        }

        if (_countChangePopRect != null)
        {
            _countPopBasePos = _countChangePopRect.anchoredPosition;
            _countPopBaseCaptured = true;
            _countChangePopRect.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (_openButton != null) _openButton.onClick.AddListener(HandleBagButtonClicked);
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        if (_cancel != null)
        {
            _cancel.performed += HandleCancel;
            _cancel.Enable();
        }
        if (_toggleKey != null)
        {
            _toggleKey.performed += HandleToggleKey;
            _toggleKey.Enable();
        }
        if (_state != null) _state.OnChanged += Refresh;
        BindPhases();
        BindInventory();
        Refresh();
    }

    private void Start()
    {
        _itemUse = GetComponent<ItemUseController>();
        BindPhases();
        EnsureReady();
        BindInventory();
        Refresh();
    }

    private void OnDisable()
    {
        if (_openButton != null) _openButton.onClick.RemoveListener(HandleBagButtonClicked);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_cancel != null)
        {
            _cancel.performed -= HandleCancel;
            _cancel.Disable();
        }
        if (_toggleKey != null)
        {
            _toggleKey.performed -= HandleToggleKey;
            _toggleKey.Disable();
        }
        _panelTween?.Kill();
        _popTween?.Kill();
        _buttonPunchTween?.Kill();
        _bagContentTween?.Kill();
        Close();
        if (_state != null) _state.OnChanged -= Refresh;
        UnbindInventory();
        UnbindPhases();
    }

    private void OnDestroy()
    {
        _panelTween?.Kill();
        _popTween?.Kill();
        _buttonPunchTween?.Kill();
        _bagContentTween?.Kill();
        _state?.Dispose();
        _cancel?.Dispose();
        _toggleKey?.Dispose();
    }

    public void SetAimingItem(ItemDefinition item)
    {
        _aimingItem = item;
        RefreshBackpackButtonVisual();
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
        AudioManager.Instance.PlayUiPopupOpen();
    }

    public void Close()
    {
        bool hadOpen = IsOpen;
        _state?.Close();
        if (_phases != null) _phases.InputGate.Release(this, Time.frameCount);
        if (hadOpen) AudioManager.Instance.PlayUiPopupClose();
        Refresh();
    }

    private void HandleBagButtonClicked()
    {
        // Clicking the aiming backpack button (red X badge) cancels aiming cleanly.
        if (_itemUse != null && _itemUse.IsTargeting)
        {
            AudioManager.Instance.PlayUiPopupClose();
            _itemUse.Cancel();
            return;
        }
        Toggle();
    }

    private void HandleToggleKey(InputAction.CallbackContext context)
    {
        if (!isActiveAndEnabled) return;
        if (_itemUse != null && _itemUse.IsTargeting)
        {
            AudioManager.Instance.PlayUiPopupClose();
            _itemUse.Cancel();
            return;
        }
        Toggle();
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

    private void BindInventory()
    {
        if (_inventory == null || !_inventory.EnsureInitialized()) return;
        ItemInventory inv = _inventory.Inventory;
        if (_subscribedInventory == inv) return;

        UnbindInventory();
        _subscribedInventory = inv;
        if (_subscribedInventory != null)
            _subscribedInventory.OnQuantityChanged += HandleInventoryQuantityChanged;
    }

    private void UnbindInventory()
    {
        if (_subscribedInventory == null) return;
        _subscribedInventory.OnQuantityChanged -= HandleInventoryQuantityChanged;
        _subscribedInventory = null;
    }

    private void HandleInventoryQuantityChanged(string id, int before, int after)
    {
        int delta = after - before;
        RefreshTotalCountBadge();
        if (delta != 0)
            PlayCountChangePop(delta);
    }

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

        BindInventory();
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
        if (_openButton != null)
        {
            _openButton.interactable = isActiveAndEnabled && _phases != null
                && (_phases.CurrentPhase == PhaseType.Day || _phases.CurrentPhase == PhaseType.Night);
        }

        RefreshBackpackButtonVisual();

        if (!show)
        {
            HidePanel();
            return;
        }

        ShowPanel();
    }

    private void ShowPanel()
    {
        bool justOpened = !_wasOpen;
        _wasOpen = true;
        _panel.SetActive(true);

        for (int i = 0; i < _slots.Length; i++)
        {
            ItemDefinition item = _inventory.Catalog[i];
            _slots[i].Bind(item, _inventory.Inventory.GetQuantity(item.Id), _state.SelectedId == item.Id, Choose);
        }

        if (justOpened)
        {
            _panelTween?.Kill();
            Transform panelTf = _panel.transform;
            panelTf.localScale = Vector3.one * 0.86f;
            _panelTween = panelTf.DOScale(1f, 0.2f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(_panel);
        }

        if (_selectionText == null) return;

        ItemDefinition selected = SelectedItem;
        _selectionText.text = selected != null
            ? selected.Title + " selected."
            : "Choose an item. Right-click or Escape to close.";
    }

    private void HidePanel()
    {
        _wasOpen = false;
        _panelTween?.Kill();
        ClearPanelSelection();
        ClearSlots();
        if (HasSeparatePanel())
        {
            _panel.transform.localScale = Vector3.one;
            _panel.SetActive(false);
        }
    }

    private void RefreshBackpackButtonVisual()
    {
        bool isAiming = _aimingItem != null;

        if (_bagButtonImage != null)
        {
            Sprite chosen = _bagClosedSprite;
            if (isAiming && _bagAimingSprite != null) chosen = _bagAimingSprite;
            else if (IsOpen && _bagOpenSprite != null) chosen = _bagOpenSprite;
            if (chosen != null) _bagButtonImage.sprite = chosen;
        }

        if (_bagButtonIcon != null)
        {
            Sprite icon = isAiming && _aimingItem.Icon != null ? _aimingItem.Icon : _defaultBackpackIcon;
            if (icon != null)
            {
                _bagButtonIcon.sprite = icon;
                _bagButtonIcon.enabled = true;
            }
        }

        if (_bagButtonContent != null)
        {
            if (!_bagContentBaseCaptured)
            {
                _bagContentBasePos = _bagButtonContent.anchoredPosition;
                _bagContentBaseCaptured = true;
            }
            bool lifted = isAiming || IsOpen;
            Vector2 targetPos = _bagContentBasePos + (lifted ? _bagOpenContentOffset : Vector2.zero);
            _bagContentTween?.Kill();
            if (isActiveAndEnabled && _bagButtonContent.gameObject.activeInHierarchy)
            {
                _bagContentTween = _bagButtonContent.DOAnchorPos(targetPos, 0.12f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetLink(_bagButtonContent.gameObject);
            }
            else
            {
                _bagButtonContent.anchoredPosition = targetPos;
            }
        }

        RefreshTotalCountBadge();
    }

    private void RefreshTotalCountBadge()
    {
        int total = GetTotalItemCount();
        bool showBadge = _aimingItem == null;

        if (_totalCountBadge != null)
            _totalCountBadge.SetActive(showBadge);

        if (_totalCountText != null)
            _totalCountText.text = total.ToString();
    }

    private int GetTotalItemCount()
    {
        if (_inventory == null || !_inventory.IsInitialized || _inventory.Catalog == null) return 0;
        int total = 0;
        for (int i = 0; i < _inventory.Catalog.Count; i++)
        {
            ItemDefinition item = _inventory.Catalog[i];
            if (item != null) total += _inventory.Inventory.GetQuantity(item.Id);
        }
        return total;
    }

    private void PlayCountChangePop(int delta)
    {
        if (_openButton != null)
        {
            _buttonPunchTween?.Kill();
            _openButton.transform.localScale = Vector3.one;
            _buttonPunchTween = _openButton.transform.DOPunchScale(Vector3.one * 0.12f, 0.24f, 4, 0.5f)
                .SetUpdate(true)
                .SetLink(_openButton.gameObject);
        }

        if (_countChangePopRect == null || _countChangePopText == null) return;

        if (!_countPopBaseCaptured)
        {
            _countPopBasePos = _countChangePopRect.anchoredPosition;
            _countPopBaseCaptured = true;
        }

        _popTween?.Kill();
        _countChangePopRect.gameObject.SetActive(true);
        _countChangePopRect.anchoredPosition = _countPopBasePos;
        _countChangePopRect.localScale = Vector3.one * 0.7f;
        _countChangePopText.text = delta > 0 ? $"+{delta}" : delta.ToString();
        _countChangePopText.color = delta > 0 ? PlusColor : MinusColor;
        if (_countChangePopGroup != null) _countChangePopGroup.alpha = 1f;

        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(_countChangePopRect.gameObject);
        seq.Append(_countChangePopRect.DOScale(1.15f, 0.14f).SetEase(Ease.OutBack));
        seq.Join(_countChangePopRect.DOAnchorPosY(_countPopBasePos.y + 30f, 0.8f).SetEase(Ease.OutCubic));
        if (_countChangePopGroup != null)
            seq.Insert(0.35f, _countChangePopGroup.DOFade(0f, 0.45f));
        seq.OnComplete(() =>
        {
            if (_countChangePopRect != null)
                _countChangePopRect.gameObject.SetActive(false);
        });
        _popTween = seq;
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
