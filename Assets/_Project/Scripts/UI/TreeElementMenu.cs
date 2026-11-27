using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TreeElementMenu : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Button _waterButton;
    [SerializeField] private Button _fireButton;
    [SerializeField] private Button _lightningButton;
    [SerializeField] private SpiritCommander _commander;
    [SerializeField] private SpiritSelection _selection;

    [Header("Element Hover Image GameObjects")]
    [Tooltip("Assign GameObject Image dari scene yang muncul saat hover tombol Water.")]
    [SerializeField] private GameObject _waterHoverImage;
    [Tooltip("Assign GameObject Image dari scene yang muncul saat hover tombol Fire.")]
    [SerializeField] private GameObject _fireHoverImage;
    [Tooltip("Assign GameObject Image dari scene yang muncul saat hover tombol Lightning.")]
    [SerializeField] private GameObject _lightningHoverImage;
    [Tooltip("Assign BaseImage agar berganti saat tombol elemen di-hover.")]
    [SerializeField] private GameObject _defaultElementImage;

    [Header("Optional Legacy UI References")]
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _powerText;
    [SerializeField] private Button _healButton;
    [SerializeField] private Button _closeButton;

    private TreeNode _tree;
    private TreeRegistry _registry;
    private PhaseManager _phases;
    private string _feedback = "";

    private void Awake()
    {
        // Pastikan gambar visual tidak memblokir klik/hover ke tombol
        DisableRaycastOnVisual(_defaultElementImage);
        DisableRaycastOnVisual(_waterHoverImage);
        DisableRaycastOnVisual(_fireHoverImage);
        DisableRaycastOnVisual(_lightningHoverImage);

        // Pastikan tombol elemen tetap bisa menangkap hover & klik meski dibuat transparan
        EnsureButtonRaycastable(_waterButton);
        EnsureButtonRaycastable(_fireButton);
        EnsureButtonRaycastable(_lightningButton);

        if (_waterButton != null) _waterButton.onClick.AddListener(ChooseWater);
        if (_fireButton != null) _fireButton.onClick.AddListener(ChooseFire);
        if (_lightningButton != null) _lightningButton.onClick.AddListener(ChooseLightning);
        if (_healButton != null) _healButton.onClick.AddListener(Heal);
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);

        BindElementButtonHover(_waterButton, ElementType.Water);
        BindElementButtonHover(_fireButton, ElementType.Fire);
        BindElementButtonHover(_lightningButton, ElementType.Lightning);
        BindButtonHover(_healButton);
        BindButtonHover(_closeButton);

        SetElementHoverVisual(ElementType.None);
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    private static void DisableRaycastOnVisual(GameObject visualObj)
    {
        if (visualObj == null) return;
        Graphic[] graphics = visualObj.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }

    private static void EnsureButtonRaycastable(Button button)
    {
        if (button == null) return;
        button.interactable = true;

        Graphic graphic = button.targetGraphic != null
            ? button.targetGraphic
            : button.GetComponent<Graphic>();

        if (graphic == null)
        {
            Image invisibleHitbox = button.gameObject.AddComponent<Image>();
            invisibleHitbox.color = new Color(1f, 1f, 1f, 0f);
            invisibleHitbox.raycastTarget = true;
            button.targetGraphic = invisibleHitbox;
        }
        else
        {
            if (!graphic.enabled)
            {
                graphic.enabled = true;
                Color c = graphic.color;
                c.a = 0f;
                graphic.color = c;
            }
            graphic.raycastTarget = true;
        }
    }

    private void BindElementButtonHover(Button button, ElementType element)
    {
        if (button == null) return;
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ =>
        {
            if (button.interactable)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayUiHover();
                SetElementHoverVisual(element);
            }
        });
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => SetElementHoverVisual(ElementType.None));
        trigger.triggers.Add(exitEntry);
    }

    private void BindButtonHover(Button button)
    {
        if (button == null) return;
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            if (button.interactable && AudioManager.Instance != null)
                AudioManager.Instance.PlayUiHover();
        });
        trigger.triggers.Add(entry);
    }

    private void SetElementHoverVisual(ElementType hoveredElement)
    {
        if (_waterHoverImage != null)
            _waterHoverImage.SetActive(hoveredElement == ElementType.Water);
        if (_fireHoverImage != null)
            _fireHoverImage.SetActive(hoveredElement == ElementType.Fire);
        if (_lightningHoverImage != null)
            _lightningHoverImage.SetActive(hoveredElement == ElementType.Lightning);
        if (_defaultElementImage != null)
            _defaultElementImage.SetActive(hoveredElement == ElementType.None);
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
        if (_phases != null) _phases.OnRewardStart -= HandleRewardStart;
        if (_registry != null)
            _registry.OnPowerChanged -= Refresh;
        if (_tree != null)
        {
            _tree.OnAvailabilityChanged -= HandleTreeAvailabilityChanged;
            if (_tree.Data != null)
                _tree.Data.OnHpChanged -= Refresh;
        }
        if (_waterButton != null) _waterButton.onClick.RemoveListener(ChooseWater);
        if (_fireButton != null) _fireButton.onClick.RemoveListener(ChooseFire);
        if (_lightningButton != null) _lightningButton.onClick.RemoveListener(ChooseLightning);
        if (_healButton != null) _healButton.onClick.RemoveListener(Heal);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
    }

    public void Open(TreeNode tree, Vector2 screenPosition)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (tree == null || !tree.IsTargetable || !tree.Data.IsFullyGrown)
            return;

        if (_panel != null && _panel.gameObject.activeSelf && _tree == tree)
        {
            Close();
            return;
        }

        CloseInternal(false);
        _tree = tree;
        _tree.OnAvailabilityChanged += HandleTreeAvailabilityChanged;
        _tree.Data.OnHpChanged += Refresh;
        _feedback = "";

        if (_canvas != null && _panel != null)
        {
            RectTransform canvasRect = (RectTransform)_canvas.transform;
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
            {
                Vector2 half = _panel.rect.size * 0.5f;
                Rect bounds = canvasRect.rect;
                _panel.anchoredPosition = new Vector2(
                    Mathf.Clamp(localPoint.x, bounds.xMin + half.x, bounds.xMax - half.x),
                    Mathf.Clamp(localPoint.y, bounds.yMin + half.y, bounds.yMax - half.y));
            }
        }

        SetElementHoverVisual(ElementType.None);
        if (_panel != null) _panel.gameObject.SetActive(true);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUiPopupOpen();
        Refresh();
    }

    public void Close()
    {
        CloseInternal(true);
    }

    private void CloseInternal(bool playCloseSound)
    {
        bool wasOpen = _panel != null && _panel.gameObject.activeSelf;

        if (_tree != null)
        {
            _tree.OnAvailabilityChanged -= HandleTreeAvailabilityChanged;
            if (_tree.Data != null)
                _tree.Data.OnHpChanged -= Refresh;
        }
        _tree = null;
        SetElementHoverVisual(ElementType.None);
        if (_panel != null) _panel.gameObject.SetActive(false);

        if (playCloseSound && wasOpen && AudioManager.Instance != null)
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
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        if (_tree.Data.Element == ElementType.None)
        {
            if (_tree.Data.TryAssignElement(element, _registry.Settings))
            {
                CloseInternal(false);
            }
            else
            {
                _feedback = "Cannot assign element";
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayUiActionBlocked();
                Refresh();
            }
            return;
        }

        if (_selection == null || _selection.Selected.Count == 0)
        {
            _feedback = "Select a spirit to reassign this tree";
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        TreeElementInteractable work = _tree.GetComponent<TreeElementInteractable>();
        if (work == null || !work.Queue(element))
        {
            _feedback = "Cannot reassign element";
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUiClick();
        if (_commander != null)
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
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        TreeHealInteractable work = _tree.GetComponent<TreeHealInteractable>();
        if (work == null || !work.CanInteract)
        {
            _feedback = "Tree does not need healing";
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayUiActionBlocked();
            Refresh();
            return;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayUiClick();
        if (_commander != null)
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
            _healButton.interactable =
                _tree.Data.NeedsHealing &&
                _selection != null && _selection.Selected.Count > 0;
        }

        if (_registry != null && _powerText != null)
        {
            _powerText.text =
                $"Water: {_registry.GetTotalPower(ElementType.Water)}\n" +
                $"Fire: {_registry.GetTotalPower(ElementType.Fire)}\n" +
                $"Lightning: {_registry.GetTotalPower(ElementType.Lightning)}";
        }
    }
}