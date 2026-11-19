using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives a button's 3+ visual states (Normal, Hover, Pressed, optional Disabled) using
// separate sprites whose pixel dimensions differ (e.g., hover sprites with outer corner brackets
// or shorter pressed sprites). The visual child resizes proportionally to the sprite ratio
// so the button's raycast hitbox never shifts or flickers.
[RequireComponent(typeof(Button))]
public class UIStateButton : UIBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Targets")]
    [SerializeField] private Image _visual;
    [SerializeField] private RectTransform _content;

    [Header("State Sprites")]
    [SerializeField] private Sprite _defaultSprite;
    [SerializeField] private Sprite _hoverSprite;
    [SerializeField] private Sprite _pressedSprite;
    [SerializeField] private Sprite _disabledSprite;

    [Header("Tactile Feel")]
    [SerializeField] private Vector2 _pressedContentOffset = new Vector2(0f, -4f);
    [SerializeField] private Color _disabledTint = new Color(0.7f, 0.7f, 0.7f, 0.85f);

    private Button _button;
    private RectTransform _rootRect;
    private RectTransform _visualRect;
    private Vector2 _contentRestPos;
    private bool _contentRestCaptured;
    private bool _isHovered;
    private bool _isSelected;
    private bool _isPressed;
    private bool _selectedByPointer;

    protected override void Awake()
    {
        base.Awake();
        CacheReferences();
        CaptureRestPositions();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheReferences();
        CaptureRestPositions();
        _isHovered = false;
        _isSelected = false;
        _isPressed = false;
        ApplyState();
    }

    protected override void OnDisable()
    {
        _isHovered = false;
        _isSelected = false;
        _isPressed = false;
        ApplyState();
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        ApplyState();
    }

    // Fires automatically when a parent CanvasGroup toggles interactable (e.g., modal pop-up opens).
    protected override void OnCanvasGroupChanged()
    {
        base.OnCanvasGroupChanged();
        if (!IsInteractable())
        {
            _isHovered = false;
            _isSelected = false;
            _isPressed = false;
        }
        ApplyState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        // Mouse hover takes priority over any prior keyboard focus so two buttons never highlight at once.
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        _isHovered = true;
        ApplyState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        // Clicking a Unity Button selects it; clear that pointer-driven selection on exit so hover art vanishes.
        if (_selectedByPointer && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        _selectedByPointer = false;
        ApplyState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
        _isPressed = true;
        _selectedByPointer = true;
        ApplyState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        _isPressed = false;
        ApplyState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!IsInteractable()) return;
        _isSelected = true;
        ApplyState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        _isPressed = false;
        _selectedByPointer = false;
        ApplyState();
    }

    public void Refresh() => ApplyState();

    private void CacheReferences()
    {
        if (_button == null) _button = GetComponent<Button>();
        if (_rootRect == null) _rootRect = (RectTransform)transform;
        if (_visual != null && _visualRect == null) _visualRect = _visual.rectTransform;
    }

    private void CaptureRestPositions()
    {
        if (_content != null && !_contentRestCaptured)
        {
            _contentRestPos = _content.anchoredPosition;
            _contentRestCaptured = true;
        }
    }

    private bool IsInteractable() => _button != null && _button.IsInteractable();

    private void ApplyState()
    {
        if (_visual == null || _defaultSprite == null) return;
        CacheReferences();

        bool interactable = IsInteractable();
        Sprite sprite;
        Color tint = Color.white;
        bool pressed = false;

        if (!interactable)
        {
            sprite = _disabledSprite != null ? _disabledSprite : _defaultSprite;
            tint = _disabledSprite != null ? Color.white : _disabledTint;
        }
        else if (_isPressed)
        {
            sprite = _pressedSprite != null ? _pressedSprite : _defaultSprite;
            pressed = true;
        }
        else if (_isHovered || _isSelected)
        {
            sprite = _hoverSprite != null ? _hoverSprite : _defaultSprite;
        }
        else
        {
            sprite = _defaultSprite;
        }

        _visual.sprite = sprite;
        _visual.color = tint;

        // Scale the visual child by the sprite's pixel ratio vs defaultSprite so outer brackets
        // expand outward without shrinking the button body, and pressed art compresses cleanly.
        if (_visualRect != null && _rootRect != null)
        {
            Vector2 baseSize = _rootRect.rect.size;
            if (baseSize.x <= 0f || baseSize.y <= 0f) baseSize = _rootRect.sizeDelta;

            Rect defaultRect = _defaultSprite.rect;
            Rect activeRect = sprite.rect;
            float ratioX = defaultRect.width > 0f ? activeRect.width / defaultRect.width : 1f;
            float ratioY = defaultRect.height > 0f ? activeRect.height / defaultRect.height : 1f;

            _visualRect.sizeDelta = new Vector2(baseSize.x * ratioX, baseSize.y * ratioY);

            // When pressed, align the shorter sprite downward so the button visibly depresses.
            float yShift = pressed ? (activeRect.height - defaultRect.height) * 0.5f * (baseSize.y / defaultRect.height) : 0f;
            _visualRect.anchoredPosition = new Vector2(0f, yShift);
        }

        if (_content != null && _contentRestCaptured)
        {
            _content.anchoredPosition = pressed ? _contentRestPos + _pressedContentOffset : _contentRestPos;
        }
    }
}
