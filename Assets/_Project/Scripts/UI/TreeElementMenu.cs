using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TreeElementMenu : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _powerText;
    [SerializeField] private Button _waterButton;
    [SerializeField] private Button _fireButton;
    [SerializeField] private Button _lightningButton;
    [SerializeField] private Button _healButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private SpiritCommander _commander;
    [SerializeField] private SpiritSelection _selection;

    [Header("UI v2 Tooltip & Radial Picker")]
    [SerializeField] private Button _dismissBackdrop;
    [SerializeField] private Image _tooltipIcon;
    [SerializeField] private TMP_Text _tooltipTitleText;
    [SerializeField] private TMP_Text _tooltipStrongText;
    [SerializeField] private TMP_Text _tooltipBonusText;
    [SerializeField] private Sprite _waterIconSprite;
    [SerializeField] private Sprite _fireIconSprite;
    [SerializeField] private Sprite _lightningIconSprite;
    [SerializeField] private Image _waterSlotImage;
    [SerializeField] private Image _fireSlotImage;
    [SerializeField] private Image _lightningSlotImage;
    [SerializeField] private Sprite _waterDefaultSprite;
    [SerializeField] private Sprite _waterHoverSprite;
    [SerializeField] private Sprite _fireDefaultSprite;
    [SerializeField] private Sprite _fireHoverSprite;
    [SerializeField] private Sprite _lightningDefaultSprite;
    [SerializeField] private Sprite _lightningHoverSprite;

    private static readonly Color WaterTitleColor = new Color(0.45f, 0.85f, 1f, 1f);
    private static readonly Color FireTitleColor = new Color(1f, 0.56f, 0.30f, 1f);
    private static readonly Color LightningTitleColor = new Color(1f, 0.88f, 0.28f, 1f);

    private TreeNode _tree;
    private TreeRegistry _registry;
    private PhaseManager _phases;
    private ElementType _previewElement = ElementType.Water;
    private string _feedback = "";
    private Tween _openTween;
    private InputAction _cancelAction;

    private void Awake()
    {
        if (_waterButton != null) _waterButton.onClick.AddListener(ChooseWater);
        if (_fireButton != null) _fireButton.onClick.AddListener(ChooseFire);
        if (_lightningButton != null) _lightningButton.onClick.AddListener(ChooseLightning);
        if (_healButton != null) _healButton.onClick.AddListener(Heal);
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        if (_dismissBackdrop != null) _dismissBackdrop.onClick.AddListener(Close);

        BindElementHover(_waterButton, ElementType.Water);
        BindElementHover(_fireButton, ElementType.Fire);
        BindElementHover(_lightningButton, ElementType.Lightning);
        BindButtonHover(_healButton);
        BindButtonHover(_closeButton);

        _cancelAction = new InputAction("CancelTreeElementMenu", InputActionType.Button);
        _cancelAction.AddBinding("<Keyboard>/escape");
        _cancelAction.AddBinding("<Mouse>/rightButton");

        if (_panel != null) _panel.gameObject.SetActive(false);
        if (_dismissBackdrop != null) _dismissBackdrop.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (_cancelAction != null)
        {
            _cancelAction.performed += HandleCancelInput;
            _cancelAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (_cancelAction != null)
        {
            _cancelAction.performed -= HandleCancelInput;
            _cancelAction.Disable();
        }
        _openTween?.Kill();
    }

    private void BindElementHover(Button button, ElementType element)
    {
        if (button == null) return;
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            if (button.interactable)
                AudioManager.Instance.PlayUiHover();
            SetPreviewElement(element, animate: true);
        });
        trigger.triggers.Add(enter);
    }

    private void BindButtonHover(Button button)
    {
        if (button == null) return;
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            if (button.interactable)
                AudioManager.Instance.PlayUiHover();
        });
        trigger.triggers.Add(entry);
    }

    private void Start()
    {
        _phases = PhaseManager.Instance;
        if (_phases != null) _phases.OnRewardStart += HandleRewardStart;
        _registry = TreeRegistry.Instance;
        if (_registry != null)
            _registry.OnPowerChanged += Refresh;
    }

    private void OnDestroy()
    {
        _openTween?.Kill();
        _cancelAction?.Dispose();

        if (_phases != null) _phases.OnRewardStart -= HandleRewardStart;
        if (_registry != null)
            _registry.OnPowerChanged -= Refresh;
        if (_tree != null)
        {
            _tree.OnAvailabilityChanged -= HandleTreeAvailabilityChanged;
            _tree.Data.OnHpChanged -= Refresh;
        }
        if (_waterButton != null) _waterButton.onClick.RemoveListener(ChooseWater);
        if (_fireButton != null) _fireButton.onClick.RemoveListener(ChooseFire);
        if (_lightningButton != null) _lightningButton.onClick.RemoveListener(ChooseLightning);
        if (_healButton != null) _healButton.onClick.RemoveListener(Heal);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_dismissBackdrop != null) _dismissBackdrop.onClick.RemoveListener(Close);
    }

    private void HandleCancelInput(InputAction.CallbackContext ctx)
    {
        if (_panel != null && _panel.gameObject.activeSelf)
            Close();
    }

    public void Open(TreeNode tree, Vector2 screenPosition)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (tree == null || !tree.IsTargetable || !tree.Data.IsFullyGrown)
            return;

        CloseInternal(false);
        _tree = tree;
        _tree.OnAvailabilityChanged += HandleTreeAvailabilityChanged;
        _tree.Data.OnHpChanged += Refresh;
        _feedback = "";
        _previewElement = _tree.Data.Element != ElementType.None
            ? _tree.Data.Element
            : ElementType.Water;

        RectTransform canvasRect = (RectTransform)_canvas.transform;
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
        {
            // Offset slightly upward so the radial ring centers cleanly around the clicked tree
            localPoint.y += 55f;
            Vector2 half = _panel.rect.size * 0.5f;
            Rect bounds = canvasRect.rect;
            const float margin = 20f;
            _panel.anchoredPosition = new Vector2(
                Mathf.Clamp(localPoint.x, bounds.xMin + half.x + margin, bounds.xMax - half.x - margin),
                Mathf.Clamp(localPoint.y, bounds.yMin + half.y + margin, bounds.yMax - half.y - margin));
        }

        if (_dismissBackdrop != null) _dismissBackdrop.gameObject.SetActive(true);
        _panel.gameObject.SetActive(true);

        _openTween?.Kill();
        _panel.localScale = Vector3.one * 0.82f;
        _openTween = _panel.DOScale(1f, 0.22f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(_panel.gameObject);

        AudioManager.Instance.PlayUiPopupOpen();
        SetPreviewElement(_previewElement, animate: false);
        Refresh();
    }

    public void Close()
    {
        CloseInternal(true);
    }

    private void CloseInternal(bool playCloseSound)
    {
        bool wasOpen = _panel != null && _panel.gameObject.activeSelf;

        _openTween?.Kill();
        if (_tree != null)
        {
            _tree.OnAvailabilityChanged -= HandleTreeAvailabilityChanged;
            _tree.Data.OnHpChanged -= Refresh;
        }
        _tree = null;
        if (_panel != null)
        {
            _panel.localScale = Vector3.one;
            _panel.gameObject.SetActive(false);
        }
        if (_dismissBackdrop != null) _dismissBackdrop.gameObject.SetActive(false);

        if (playCloseSound && wasOpen)
            AudioManager.Instance.PlayUiPopupClose();
    }

    private void ChooseWater() => Choose(ElementType.Water);
    private void ChooseFire() => Choose(ElementType.Fire);
    private void ChooseLightning() => Choose(ElementType.Lightning);

    private void Choose(ElementType element)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed)
        {
            CloseInternal(false);
            return;
        }
        if (_tree == null || _registry == null || _registry.Settings == null)
            return;

        if (_tree.Data.Element == element)
        {
            _feedback = "Tree already has that element";
            AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        if (_tree.Data.Element == ElementType.None)
        {
            if (_tree.Data.TryAssignElement(element, _registry.Settings))
            {
                // SFX transformasi elemen diputar otomatis oleh TreeNode.HandleStageUpgraded()
                CloseInternal(false);
                return;
            }

            _feedback = "Cannot assign element";
            AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        if (_selection == null || _selection.Selected.Count == 0)
        {
            _feedback = "Select a spirit to reassign this tree";
            AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        TreeElementInteractable work = _tree.GetComponent<TreeElementInteractable>();
        if (work == null || !work.Queue(element))
        {
            _feedback = "Cannot reassign element";
            AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        AudioManager.Instance.PlayUiClick();
        _commander.InteractSelected(work);
        CloseInternal(false);
    }

    private void Heal()
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed)
        {
            CloseInternal(false);
            return;
        }
        if (_tree == null) return;

        if (_selection == null || _selection.Selected.Count == 0)
        {
            _feedback = "Select a spirit to heal this tree";
            AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        TreeHealInteractable work = _tree.GetComponent<TreeHealInteractable>();
        if (work == null || !work.CanInteract)
        {
            _feedback = "Tree does not need healing";
            AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        AudioManager.Instance.PlayUiClick();
        _commander.InteractSelected(work);
        CloseInternal(false);
    }

    private void HandleRewardStart(int night) => CloseInternal(false);

    private void HandleTreeAvailabilityChanged(TreeNode tree)
    {
        if (tree == null || !tree.IsTargetable)
            CloseInternal(false);
        else
            Refresh();
    }

    private void SetPreviewElement(ElementType element, bool animate)
    {
        _previewElement = element;
        ApplySlotVisual(_waterSlotImage, _waterButton, _waterDefaultSprite, _waterHoverSprite, element == ElementType.Water, animate);
        ApplySlotVisual(_fireSlotImage, _fireButton, _fireDefaultSprite, _fireHoverSprite, element == ElementType.Fire, animate);
        ApplySlotVisual(_lightningSlotImage, _lightningButton, _lightningDefaultSprite, _lightningHoverSprite, element == ElementType.Lightning, animate);
        RefreshTooltip();
    }

    private static void ApplySlotVisual(Image img, Button btn, Sprite normalSprite, Sprite hoverSprite, bool highlighted, bool animate)
    {
        if (img != null)
        {
            Sprite chosen = highlighted && hoverSprite != null ? hoverSprite : normalSprite;
            if (chosen != null) img.sprite = chosen;
        }

        if (btn == null) return;
        float targetScale = highlighted ? 1.09f : 1f;
        btn.transform.DOKill();
        if (animate)
        {
            btn.transform.DOScale(targetScale, 0.14f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(btn.gameObject);
        }
        else
        {
            btn.transform.localScale = Vector3.one * targetScale;
        }
    }

    private void RefreshTooltip()
    {
        int powerBonus = 10;
        if (_tree != null && _tree.Data != null && _tree.Data.Definition != null)
        {
            powerBonus = _tree.Data.Definition.ElementPower > 0
                ? _tree.Data.Definition.ElementPower
                : (_tree.Data.Definition.Type == TreeType.Tumbang ? 15 : 10);
        }

        string title;
        string strongVs;
        string powerName;
        Color titleColor;
        Sprite icon;

        switch (_previewElement)
        {
            case ElementType.Fire:
                title = "FIRE TREE";
                strongVs = "Strong vs LIGHTNING (x2 damage)";
                powerName = "Fire";
                titleColor = FireTitleColor;
                icon = _fireIconSprite;
                break;
            case ElementType.Lightning:
                title = "LIGHTNING TREE";
                strongVs = "Strong vs WATER (x2 damage)";
                powerName = "Lightning";
                titleColor = LightningTitleColor;
                icon = _lightningIconSprite;
                break;
            default:
                title = "WATER TREE";
                strongVs = "Strong vs FIRE (x2 damage)";
                powerName = "Water";
                titleColor = WaterTitleColor;
                icon = _waterIconSprite;
                break;
        }

        if (_tooltipIcon != null && icon != null)
            _tooltipIcon.sprite = icon;

        if (_tooltipTitleText != null)
        {
            _tooltipTitleText.text = title;
            _tooltipTitleText.color = titleColor;
        }

        if (_tooltipStrongText != null)
            _tooltipStrongText.text = strongVs;

        if (_tooltipBonusText != null)
        {
            _tooltipBonusText.text = string.IsNullOrEmpty(_feedback)
                ? $"+{powerBonus} {powerName} Power"
                : _feedback;
        }
    }

    private void Refresh()
    {
        if (_tree == null || _panel == null || !_panel.gameObject.activeSelf) return;

        if (_statusText != null)
        {
            _statusText.text =
                $"Tree: {_tree.Data.Element}\n" +
                $"HP: {_tree.Data.Hp:0}/{_tree.Data.MaxHp:0}\n" +
                _feedback;
        }

        if (_healButton != null)
        {
            bool needsHeal = _tree.Data.NeedsHealing;
            _healButton.gameObject.SetActive(needsHeal);
            _healButton.interactable = needsHeal && _selection != null && _selection.Selected.Count > 0;
        }

        if (_powerText != null && _registry != null)
        {
            _powerText.text =
                $"Water: {_registry.GetTotalPower(ElementType.Water)}\n" +
                $"Fire: {_registry.GetTotalPower(ElementType.Fire)}\n" +
                $"Lightning: {_registry.GetTotalPower(ElementType.Lightning)}";
        }

        RefreshTooltip();
    }
}
