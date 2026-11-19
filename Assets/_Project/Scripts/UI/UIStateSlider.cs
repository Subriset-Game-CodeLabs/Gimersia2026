using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives a slider's 3 visual states (Normal, Hover, Dragging/Clicked) on its wooden handle,
// reveals the leafy fill image without stretching, and keeps the optional percentage label synced.
[RequireComponent(typeof(Slider))]
public class UIStateSlider : UIBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IEndDragHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("Targets")]
    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _handleImage;
    [SerializeField] private TMP_Text _percentText;

    [Header("Handle State Sprites")]
    [SerializeField] private Sprite _defaultHandle;
    [SerializeField] private Sprite _hoverHandle;
    [SerializeField] private Sprite _draggingHandle;

    private Slider _slider;
    private Vector2 _handleBaseSize;
    private bool _handleBaseCaptured;
    private bool _isHovered;
    private bool _isSelected;
    private bool _isDragging;
    private bool _selectedByPointer;

    protected override void Awake()
    {
        base.Awake();
        CacheReferences();
        if (_slider != null) _slider.onValueChanged.AddListener(HandleValueChanged);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheReferences();
        _isHovered = false;
        _isSelected = false;
        _isDragging = false;
        Refresh();
    }

    protected override void OnDisable()
    {
        _isHovered = false;
        _isSelected = false;
        _isDragging = false;
        ApplyHandleState();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        if (_slider != null) _slider.onValueChanged.RemoveListener(HandleValueChanged);
        base.OnDestroy();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        _isHovered = true;
        ApplyHandleState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        if (_selectedByPointer && !_isDragging && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
            _selectedByPointer = false;
        }
        ApplyHandleState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !IsInteractable()) return;
        _isDragging = true;
        _selectedByPointer = true;
        ApplyHandleState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        _isDragging = false;
        if (!_isHovered && _selectedByPointer && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
            _selectedByPointer = false;
        }
        ApplyHandleState();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        _isDragging = true;
        _selectedByPointer = true;
        ApplyHandleState();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        if (!_isHovered && _selectedByPointer && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
            _selectedByPointer = false;
        }
        ApplyHandleState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!IsInteractable()) return;
        _isSelected = true;
        ApplyHandleState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        _isDragging = false;
        _selectedByPointer = false;
        ApplyHandleState();
    }

    public void Refresh()
    {
        CacheReferences();
        if (_slider != null) UpdateFillAndLabel(_slider.normalizedValue);
        ApplyHandleState();
    }

    private void CacheReferences()
    {
        if (_slider == null) _slider = GetComponent<Slider>();
        if (_handleImage != null && !_handleBaseCaptured)
        {
            _handleBaseSize = _handleImage.rectTransform.sizeDelta;
            _handleBaseCaptured = true;
        }
    }

    private bool IsInteractable() => _slider != null && _slider.IsInteractable();

    private void HandleValueChanged(float _)
    {
        if (_slider != null) UpdateFillAndLabel(_slider.normalizedValue);
    }

    private void UpdateFillAndLabel(float normalized)
    {
        if (_fillImage != null) _fillImage.fillAmount = Mathf.Clamp01(normalized);
        if (_percentText != null) _percentText.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
    }

    private void ApplyHandleState()
    {
        if (_handleImage == null || _defaultHandle == null) return;

        Sprite sprite;
        if (_isDragging)
            sprite = _draggingHandle != null ? _draggingHandle : _defaultHandle;
        else if (_isHovered || _isSelected)
            sprite = _hoverHandle != null ? _hoverHandle : _defaultHandle;
        else
            sprite = _defaultHandle;

        _handleImage.sprite = sprite;

        if (_handleBaseCaptured)
        {
            Rect defRect = _defaultHandle.rect;
            Rect actRect = sprite.rect;
            float rx = defRect.width > 0f ? actRect.width / defRect.width : 1f;
            float ry = defRect.height > 0f ? actRect.height / defRect.height : 1f;
            _handleImage.rectTransform.sizeDelta = new Vector2(_handleBaseSize.x * rx, _handleBaseSize.y * ry);
        }
    }
}
