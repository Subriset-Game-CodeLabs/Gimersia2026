using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives a Level Selection map node across Locked, Unlocked, Hover, Selected, and Completed states
// using the same stable-hitbox + proportional-visual pattern as UIStateButton, and animates both
// the wooden stump visual and number label together smoothly with DOTween.
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

    [Header("Motion (DOTween)")]
    [SerializeField] private float _hoverLiftY = 5f;
    [SerializeField] private float _selectedLiftY = 9f;
    [SerializeField] private float _hoverDuration = 0.16f;
    [SerializeField] private float _selectedDuration = 0.22f;
    [Tooltip("Vertical baseline compensation (UI px) so Hover/Selected sprites with top corner brackets keep the wooden stump aligned before lifting.")]
    [SerializeField] private float _bracketSpriteOffsetY = 5.15f;

    private Button _button;
    private RectTransform _rootRect;
    private RectTransform _visualRect;
    private RectTransform _numberRect;
    private Vector2 _visualRestPos;
    private Vector2 _numberRestPos;
    private bool _restCaptured;
    private float _currentLiftY;
    private float _activeSpriteOffsetY;
    private Tween _liftTween;
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
        ApplyState(animate: false);
    }

    protected override void OnDisable()
    {
        KillLiftTween();
        _isHovered = false;
        _isKeyboardFocused = false;
        ApplyState(animate: false);
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        KillLiftTween();
        base.OnDestroy();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        ApplyState(animate: false);
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
        ApplyState(animate: false);
    }

    public void SetSelectedNode(bool selected)
    {
        _selectedNode = selected;
        ApplyState(animate: true);
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
        ApplyState(animate: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        ApplyState(animate: true);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_unlocked) return;
        _isKeyboardFocused = true;
        ApplyState(animate: true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isKeyboardFocused = false;
        ApplyState(animate: true);
    }

    private void CacheReferences()
    {
        if (_button == null) _button = GetComponent<Button>();
        if (_rootRect == null) _rootRect = (RectTransform)transform;
        if (_visual != null && _visualRect == null) _visualRect = _visual.rectTransform;
        if (_numberLabel == null) _numberLabel = GetComponentInChildren<TMP_Text>(true);
        if (_numberLabel != null)
        {
            _numberLabel.raycastTarget = false;
            if (_numberRect == null) _numberRect = _numberLabel.rectTransform;
        }

        if (!_restCaptured && (_visualRect != null || _numberRect != null))
        {
            _visualRestPos = _visualRect != null ? _visualRect.anchoredPosition : Vector2.zero;
            _numberRestPos = _numberRect != null ? _numberRect.anchoredPosition : Vector2.zero;
            _restCaptured = true;
        }
    }

    private void KillLiftTween()
    {
        if (_liftTween != null && _liftTween.IsActive())
            _liftTween.Kill();
        _liftTween = null;
    }

    private void SetLiftY(float liftY)
    {
        _currentLiftY = liftY;
        if (_visualRect != null)
            _visualRect.anchoredPosition = new Vector2(_visualRestPos.x, _visualRestPos.y + _activeSpriteOffsetY + liftY);
        if (_numberRect != null)
            _numberRect.anchoredPosition = new Vector2(_numberRestPos.x, _numberRestPos.y + liftY);
    }

    private void ApplyState(bool animate = false)
    {
        if (_visual == null || _unlockedSprite == null) return;
        CacheReferences();

        if (_numberLabel != null) _numberLabel.gameObject.SetActive(_unlocked);

        Sprite sprite;
        float targetLiftY = 0f;
        float spriteOffsetY = 0f;
        float duration = _hoverDuration;
        Ease ease = Ease.OutCubic;

        if (!_unlocked)
        {
            sprite = _lockedSprite != null ? _lockedSprite : _unlockedSprite;
        }
        else if (_selectedNode)
        {
            sprite = _selectedSprite != null ? _selectedSprite : _unlockedSprite;
            targetLiftY = _selectedLiftY;
            spriteOffsetY = sprite == _selectedSprite ? _bracketSpriteOffsetY : 0f;
            duration = _selectedDuration;
            ease = Ease.OutBack;
        }
        else if (_isHovered || _isKeyboardFocused)
        {
            sprite = _hoverSprite != null ? _hoverSprite : _unlockedSprite;
            targetLiftY = _hoverLiftY;
            spriteOffsetY = sprite == _hoverSprite ? _bracketSpriteOffsetY : 0f;
        }
        else if (_completed && _completedSprite != null)
        {
            sprite = _completedSprite;
        }
        else
        {
            sprite = _unlockedSprite;
        }

        _visual.sprite = sprite;
        _activeSpriteOffsetY = spriteOffsetY;

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

        KillLiftTween();

        if (!animate || !Application.isPlaying || !gameObject.activeInHierarchy)
        {
            SetLiftY(targetLiftY);
            return;
        }

        // Apply updated sprite baseline immediately so frame-0 stays seamless, then glide stump & number together.
        SetLiftY(_currentLiftY);
        if (!Mathf.Approximately(_currentLiftY, targetLiftY))
        {
            _liftTween = DOTween.To(() => _currentLiftY, SetLiftY, targetLiftY, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }
}
