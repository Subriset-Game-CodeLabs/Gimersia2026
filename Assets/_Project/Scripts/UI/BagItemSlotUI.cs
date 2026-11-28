using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BagItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button _button;
    [Tooltip("Optional label for expanded layouts; leave empty for icon-only slots.")]
    [SerializeField] private TMP_Text _title;
    [Tooltip("Optional label for expanded layouts; leave empty for icon-only slots.")]
    [SerializeField] private TMP_Text _description;
    [SerializeField] private TMP_Text _quantity;
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _selectedIndicator;

    [Header("UI v2 State Sprites")]
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Sprite _defaultSprite;
    [SerializeField] private Sprite _hoverSprite;
    [SerializeField] private Sprite _selectedSprite;
    [SerializeField] private Sprite _emptySprite;
    [SerializeField, Range(0.1f, 1f)] private float _emptyIconAlpha = 0.42f;

    private Action<string> _choose;
    private string _id;
    private bool _available;
    private bool _selected;
    private bool _hovered;
    private Tween _scaleTween;

    public Button Button => _button;
    public bool IsConfigured => _button != null && _button.enabled && _button.gameObject.activeSelf
        && _quantity != null;

    private void OnEnable()
    {
        if (_button != null) _button.onClick.AddListener(Choose);
    }

    private void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(Choose);
        _hovered = false;
        _scaleTween?.Kill();
        transform.localScale = Vector3.one;
    }

    public void Bind(ItemDefinition item, int quantity, bool selected, Action<string> choose)
    {
        if (!IsConfigured || item == null) { Clear(); return; }
        _id = item.Id;
        _choose = choose;
        _available = quantity > 0;
        _selected = selected && _available;
        _hovered = false;

        if (_title != null) _title.text = item.Title;
        if (_description != null) _description.text = item.Description;
        _quantity.text = quantity.ToString();
        _quantity.color = _available ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.85f);

        _button.interactable = _available;
        if (_icon != null)
        {
            _icon.sprite = item.Icon;
            _icon.enabled = item.Icon != null;
            _icon.color = _available ? Color.white : new Color(0.85f, 0.85f, 0.85f, _emptyIconAlpha);
        }

        if (_selectedIndicator != null) _selectedIndicator.SetActive(_selected);
        ApplyBackgroundSprite();
        transform.localScale = Vector3.one;
    }

    public void Clear()
    {
        _id = null;
        _choose = null;
        _available = false;
        _selected = false;
        _hovered = false;
        _scaleTween?.Kill();
        transform.localScale = Vector3.one;
        if (_button != null) _button.interactable = false;
        if (_selectedIndicator != null) _selectedIndicator.SetActive(false);
        ApplyBackgroundSprite();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_available || !isActiveAndEnabled) return;
        _hovered = true;
        ApplyBackgroundSprite();
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(1.06f, 0.12f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(gameObject);
        AudioManager.Instance.PlayUiHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        ApplyBackgroundSprite();
        _scaleTween?.Kill();
        _scaleTween = transform.DOScale(1f, 0.12f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void ApplyBackgroundSprite()
    {
        if (_backgroundImage == null) return;

        Sprite chosen;
        if (!_available)
            chosen = _emptySprite != null ? _emptySprite : _defaultSprite;
        else if (_selected && _selectedSprite != null)
            chosen = _selectedSprite;
        else if (_hovered && _hoverSprite != null)
            chosen = _hoverSprite;
        else
            chosen = _defaultSprite;

        if (chosen != null) _backgroundImage.sprite = chosen;
    }

    private void Choose()
    {
        if (!isActiveAndEnabled || !_available) return;
        AudioManager.Instance.PlayUiClick();
        _choose?.Invoke(_id);
    }
}
