using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives a Level Selection map node across Locked, Unlocked, Hover, Selected, and Completed states
// using the same stable-hitbox + proportional-visual pattern as UIStateButton.
[RequireComponent(typeof(Button))]
public class UILevelNodeButton : UIBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Targets")]
    [SerializeField] private Image _visual;
    [SerializeField] private TMP_Text _numberLabel;

    [Header("State Sprites")]
    [SerializeField] private Sprite _unlockedSprite;
    [SerializeField] private Sprite _hoverSprite;
    [SerializeField] private Sprite _selectedSprite;
    [SerializeField] private Sprite _completedSprite;
    [SerializeField] private Sprite _lockedSprite;

    private Button _button;
    private RectTransform _rootRect;
    private RectTransform _visualRect;
    private bool _unlocked;
    private bool _selectedNode;
    private bool _completed;
    private bool _isHovered;
    private bool _isKeyboardFocused;

    public Button Button
    {
        get
        {
            if (_button == null) _button = GetComponent<Button>();
            return _button;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        CacheReferences();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheReferences();
        _isHovered = false;
        _isKeyboardFocused = false;
        ApplyState();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        ApplyState();
    }

    public void Configure(int number, bool unlocked, bool selected, bool completed = false)
    {
        CacheReferences();
        _unlocked = unlocked;
        _selectedNode = selected;
        _completed = completed;

        if (_button != null) _button.interactable = unlocked;
        if (_numberLabel != null)
        {
            _numberLabel.text = number.ToString();
            _numberLabel.gameObject.SetActive(unlocked);
        }
        ApplyState();
    }

    public void SetSelectedNode(bool selected)
    {
        _selectedNode = selected;
        ApplyState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_unlocked) return;
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
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        ApplyState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_unlocked) return;
        _isKeyboardFocused = true;
        ApplyState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isKeyboardFocused = false;
        ApplyState();
    }

    private void CacheReferences()
    {
        if (_button == null) _button = GetComponent<Button>();
        if (_rootRect == null) _rootRect = (RectTransform)transform;
        if (_visual != null && _visualRect == null) _visualRect = _visual.rectTransform;
    }

    private void ApplyState()
    {
        if (_visual == null || _unlockedSprite == null) return;
        CacheReferences();

        Sprite sprite;
        if (!_unlocked)
            sprite = _lockedSprite != null ? _lockedSprite : _unlockedSprite;
        else if (_selectedNode)
            sprite = _selectedSprite != null ? _selectedSprite : _unlockedSprite;
        else if (_isHovered || _isKeyboardFocused)
            sprite = _hoverSprite != null ? _hoverSprite : _unlockedSprite;
        else if (_completed && _completedSprite != null)
            sprite = _completedSprite;
        else
            sprite = _unlockedSprite;

        _visual.sprite = sprite;

        if (_visualRect != null && _rootRect != null)
        {
            Vector2 baseSize = _rootRect.rect.size;
            if (baseSize.x <= 0f || baseSize.y <= 0f) baseSize = _rootRect.sizeDelta;

            Rect baseRect = _unlockedSprite.rect;
            Rect activeRect = sprite.rect;
            float rx = baseRect.width > 0f ? activeRect.width / baseRect.width : 1f;
            float ry = baseRect.height > 0f ? activeRect.height / baseRect.height : 1f;
            _visualRect.sizeDelta = new Vector2(baseSize.x * rx, baseSize.y * ry);
        }
    }
}
