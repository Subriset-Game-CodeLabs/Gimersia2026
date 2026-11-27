using System;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class BuffChoiceCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _cardBackground;
    [SerializeField] private TMP_Text _categoryLabel;
    [SerializeField] private TMP_Text _effectStrip;
    [Tooltip("Optional child containing only labels/icon, never the card root or background. Leave unassigned for legacy visuals.")]
    [SerializeField] private RectTransform _selectionContent;
    [Tooltip("Selected artwork compensation in UI units, relative to authored content position. Try (0, 18.3) at card height 659.6472; tune for your layout. Zero disables movement.")]
    [SerializeField] private Vector2 _selectedContentOffset = Vector2.zero;
    [Tooltip("Visual transition duration in unscaled seconds. Zero applies immediately.")]
    [SerializeField, Min(0f)] private float _selectionTweenDuration = 0.15f;
    [SerializeField] private Ease _selectionTweenEase = Ease.OutCubic;
    private RectTransform _cachedSelectionContent;
    private Vector2 _contentBaseline;
    private Tween _contentTween;
    private bool _contentSelected;
    private bool _hovered;
    private bool _selected;
    private Action<int> _choose;
    private int _index;
    private BuffDefinition _definition;
    private Sprite _originalCardSprite;
    private bool _cardBackgroundCached;
    public bool IsConfigured => _button != null && _button.enabled && _button.gameObject.activeSelf
        && _title != null && _description != null;
    public Button Button => _button;

    private void OnEnable() { if (_button != null) _button.onClick.AddListener(Choose); }
    private void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(Choose);
        _hovered = false;
        ResetSelectionContent();
        SetSelected(false);
    }

    public void Bind(BuffDefinition definition, int index, Action<int> choose, int currentStacks = 0)
    {
        _hovered = false;
        ResetSelectionContent();
        _index = index;
        _choose = choose;
        _definition = definition;
        SetSelected(false);
        _title.text = definition.Title;
        _description.text = definition.Description;
        if (_categoryLabel != null) _categoryLabel.text = definition.CategoryLabel;
        if (_effectStrip != null) _effectStrip.text = definition.EffectStrip;
        if (_icon != null)
        {
            _icon.sprite = definition.Icon;
            _icon.enabled = definition.Icon != null;
        }
        _button.interactable = true;
    }

    public void Clear()
    {
        _hovered = false;
        ResetSelectionContent();
        _definition = null;
        SetSelected(false);
        _choose = null;
        if (_button != null) _button.interactable = false;
    }

    private bool CanInteract => _definition != null && isActiveAndEnabled
        && _button != null && _button.isActiveAndEnabled && _button.IsInteractable();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_hovered || !CanInteract) return;
        _hovered = true;
        RefreshVisual();
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlayUiHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        RefreshVisual();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        CacheCardBackground();
        if (_cardBackground == null)
        {
            ResetSelectionContent();
            return;
        }
        Sprite normal = _definition != null && _definition.CardTemplate != null
            ? _definition.CardTemplate : _originalCardSprite;
        bool hasSelectedArt = (_selected || (_hovered && CanInteract)) && _definition != null && _definition.CardHoverTemplate != null;
        _cardBackground.sprite = hasSelectedArt ? _definition.CardHoverTemplate : normal;
        if (!IsValidSelectionContent(_selectionContent))
        {
            ResetSelectionContent();
            return;
        }
        if (_cachedSelectionContent != _selectionContent)
        {
            ResetSelectionContent();
            _cachedSelectionContent = _selectionContent;
            // Capture once, never from an interrupted selected animation or a rebind.
            _contentBaseline = _selectionContent.anchoredPosition;
        }
        if (_contentSelected == hasSelectedArt) return;
        _contentSelected = hasSelectedArt;
        _contentTween?.Kill();
        _contentTween = null;
        Vector2 target = _contentBaseline + (hasSelectedArt ? _selectedContentOffset : Vector2.zero);
        if (!isActiveAndEnabled || !(_selectionTweenDuration > 0f) || _selectionContent.anchoredPosition == target)
        {
            _selectionContent.anchoredPosition = target;
            return;
        }
        RectTransform content = _cachedSelectionContent;
        _contentTween = DOTween.To(() => content.anchoredPosition,
            value => content.anchoredPosition = value, target, _selectionTweenDuration)
            .SetEase(_selectionTweenEase).SetUpdate(true);
    }

    private void ResetSelectionContent()
    {
        _contentTween?.Kill();
        _contentTween = null;
        _contentSelected = false;
        if (IsValidSelectionContent(_cachedSelectionContent))
            _cachedSelectionContent.anchoredPosition = _contentBaseline;
    }

    private bool IsValidSelectionContent(RectTransform content)
    {
        // Never move the background or the root owned by the HUD entrance tween.
        return content != null && content != transform && content.IsChildOf(transform)
            && _cardBackground != null && !_cardBackground.transform.IsChildOf(content);
    }

    private void CacheCardBackground()
    {
        if (_cardBackgroundCached) return;
        if (_cardBackground == null) _cardBackground = GetComponent<Image>();
        if (_cardBackground != null)
        {
            // Preserve the authored fallback before the first template is applied.
            _originalCardSprite = _cardBackground.sprite;
            _cardBackgroundCached = true;
        }
    }

    private void Choose() { if (CanInteract) _choose?.Invoke(_index); }
}
