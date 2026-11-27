using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives a Preparation Shop item card across Default, Hover, and Selected states
// with synchronized DOTween lift animation so all icons, titles, and dynamic numbers
// move seamlessly with the wooden card frame.
public sealed class ShopItemCardUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Targets")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _cardVisual;
    [SerializeField] private RectTransform _cardContent;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _price;
    [SerializeField] private TMP_Text _owned;
    [SerializeField] private GameObject _selectedHighlight;

    [Header("State Sprites")]
    [SerializeField] private Sprite _defaultSprite;
    [SerializeField] private Sprite _hoverSprite;
    [SerializeField] private Sprite _selectedSprite;

    [Header("Motion (DOTween)")]
    [SerializeField] private float _hoverLiftY = 6f;
    [SerializeField] private float _selectedLiftY = 10f;
    [SerializeField] private float _hoverDuration = 0.16f;
    [SerializeField] private float _selectedDuration = 0.22f;
    [Tooltip("Baseline Y offset to cancel Hover sprite's built-in upward pixel shift so frame-0 stays seamless before DOTween lifts.")]
    [SerializeField] private float _hoverSpriteOffsetY = -6f;
    [Tooltip("Baseline Y offset to cancel Selected sprite's built-in upward pixel shift so frame-0 stays seamless before DOTween lifts.")]
    [SerializeField] private float _selectedSpriteOffsetY = -10f;

    private RectTransform _visualRect;
    private Vector2 _visualRestPos;
    private Vector2 _contentRestPos;
    private bool _restCaptured;
    private float _currentLiftY;
    private float _activeSpriteOffsetY;
    private Tween _liftTween;
    private Tween _ownedPunchTween;

    private Action _select;
    private bool _available;
    private bool _selected;
    private bool _isHovered;
    private bool _isKeyboardFocused;
    private bool _hasBound;
    private int _lastOwned = -1;

    public Button Button => _button;
    public bool IsConfigured => _button != null && _price != null && _owned != null
        && _selectedHighlight != null && _selectedHighlight != gameObject
        && _selectedHighlight.transform.IsChildOf(transform);

    private void Awake() => CacheReferences();

    private void OnEnable()
    {
        CacheReferences();
        if (_button != null) _button.onClick.AddListener(Choose);
        _isHovered = false;
        _isKeyboardFocused = false;
        ApplyVisualState(animate: false);
    }

    private void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(Choose);
        KillTweens();
        _isHovered = false;
        _isKeyboardFocused = false;
        ApplyVisualState(animate: false);
    }

    private void OnDestroy() => KillTweens();

    private void Choose() => _select?.Invoke();

    public void Bind(PreparationShopConfig.Entry entry, int owned, bool selected, bool available, Action select)
    {
        CacheReferences();
        bool wasSelected = _selected;
        bool ownedIncreased = _hasBound && _lastOwned >= 0 && owned > _lastOwned;

        _select = select;
        _available = available;
        _selected = selected;
        _lastOwned = owned;

        if (_title != null) _title.text = entry.Item.Title.ToUpperInvariant();
        if (_price != null) _price.text = entry.Price.ToString();
        if (_owned != null) _owned.text = $"X{owned}";
        if (_icon != null)
        {
            _icon.sprite = entry.Item.Icon;
            _icon.enabled = entry.Item.Icon != null;
        }
        if (_selectedHighlight != null) _selectedHighlight.SetActive(selected);
        if (_button != null) _button.interactable = available;

        bool animateState = _hasBound && wasSelected != selected;
        _hasBound = true;
        ApplyVisualState(animate: animateState);

        if (ownedIncreased && _owned != null && Application.isPlaying && gameObject.activeInHierarchy)
            PunchOwnedBadge();
    }

    public void Clear()
    {
        _select = null;
        _available = false;
        _hasBound = false;
        _lastOwned = -1;
        if (_button != null) _button.interactable = false;
        KillTweens();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_available) return;
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        _isHovered = true;
        AudioManager.Instance.PlayUiHover();
        ApplyVisualState(animate: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        ApplyVisualState(animate: true);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_available) return;
        _isKeyboardFocused = true;
        AudioManager.Instance.PlayUiHover();
        ApplyVisualState(animate: true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isKeyboardFocused = false;
        ApplyVisualState(animate: true);
    }

    private void CacheReferences()
    {
        if (_cardVisual != null && _visualRect == null)
            _visualRect = _cardVisual.rectTransform;

        if (!_restCaptured && (_visualRect != null || _cardContent != null))
        {
            _visualRestPos = _visualRect != null ? _visualRect.anchoredPosition : Vector2.zero;
            _contentRestPos = _cardContent != null ? _cardContent.anchoredPosition : Vector2.zero;
            _restCaptured = true;
        }
    }

    private void KillTweens()
    {
        if (_liftTween != null && _liftTween.IsActive()) _liftTween.Kill();
        _liftTween = null;

        if (_ownedPunchTween != null && _ownedPunchTween.IsActive()) _ownedPunchTween.Kill();
        _ownedPunchTween = null;
        if (_owned != null) _owned.rectTransform.localScale = Vector3.one;
    }

    private void PunchOwnedBadge()
    {
        if (_ownedPunchTween != null && _ownedPunchTween.IsActive()) _ownedPunchTween.Kill();
        RectTransform rect = _owned.rectTransform;
        rect.localScale = Vector3.one;
        _ownedPunchTween = rect.DOPunchScale(new Vector3(0.24f, 0.24f, 0f), 0.24f, 6, 0.6f)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void SetLiftY(float liftY)
    {
        _currentLiftY = liftY;
        if (_visualRect != null)
            _visualRect.anchoredPosition = new Vector2(_visualRestPos.x, _visualRestPos.y + _activeSpriteOffsetY + liftY);
        if (_cardContent != null)
            _cardContent.anchoredPosition = new Vector2(_contentRestPos.x, _contentRestPos.y + liftY);
    }

    private void ApplyVisualState(bool animate)
    {
        CacheReferences();

        Sprite sprite = _defaultSprite;
        float targetLiftY = 0f;
        float spriteOffsetY = 0f;
        float duration = _hoverDuration;
        Ease ease = Ease.OutCubic;

        if (_selected)
        {
            sprite = _selectedSprite != null ? _selectedSprite : _defaultSprite;
            targetLiftY = _selectedLiftY;
            spriteOffsetY = sprite == _selectedSprite ? _selectedSpriteOffsetY : 0f;
            duration = _selectedDuration;
            ease = Ease.OutBack;
        }
        else if (_available && (_isHovered || _isKeyboardFocused))
        {
            sprite = _hoverSprite != null ? _hoverSprite : _defaultSprite;
            targetLiftY = _hoverLiftY;
            spriteOffsetY = sprite == _hoverSprite ? _hoverSpriteOffsetY : 0f;
        }

        if (_cardVisual != null && sprite != null)
            _cardVisual.sprite = sprite;

        _activeSpriteOffsetY = spriteOffsetY;

        if (_liftTween != null && _liftTween.IsActive()) _liftTween.Kill();
        _liftTween = null;

        if (!animate || !Application.isPlaying || !gameObject.activeInHierarchy)
        {
            SetLiftY(targetLiftY);
            return;
        }

        // Apply new sprite baseline immediately so frame-0 stays seamless, then glide card + numbers together.
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
