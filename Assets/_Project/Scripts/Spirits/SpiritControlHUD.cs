using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SpiritControlHUD : MonoBehaviour
{
    [Serializable]
    public class KeyButtonSlot
    {
        public string label;
        [Tooltip("Objek visual saat tombol tidak ditekan (Default).")]
        public GameObject defaultStateObject;
        [Tooltip("Objek visual saat tombol sedang ditekan (Pressed).")]
        public GameObject pressedStateObject;

        [Header("UI v2 State Sprites & Interaction")]
        public Button button;
        public RectTransform visualRect;
        public Image targetImage;
        public Sprite defaultSprite;
        public Sprite hoverSprite;
        public Sprite selectedSprite;
        public Sprite pressedSprite;
        [SerializeField] private float _hoverLiftY = 4f;
        [SerializeField] private float _selectedLiftY = 10f;

        private bool _isPressed;
        private bool _isSelected;
        private bool _isHovered;
        private Vector2 _baseAnchoredPos;
        private bool _basePosCaptured;
        private Tween _moveTween;
        private Tween _punchTween;

        public void CaptureBasePos()
        {
            if (visualRect != null && !_basePosCaptured)
            {
                _baseAnchoredPos = visualRect.anchoredPosition;
                _basePosCaptured = true;
            }
        }

        public void SetPressed(bool isPressed)
        {
            _isPressed = isPressed;
            ApplyVisual(animate: true);

            if (isPressed && visualRect != null)
            {
                _punchTween?.Kill();
                visualRect.localScale = Vector3.one;
                _punchTween = visualRect.DOPunchScale(Vector3.one * -0.07f, 0.16f, 4, 0.5f)
                    .SetUpdate(true)
                    .SetLink(visualRect.gameObject);
            }
        }

        public void SetSelected(bool isSelected, bool animate = true)
        {
            if (_isSelected == isSelected && animate) return;
            _isSelected = isSelected;
            ApplyVisual(animate);
        }

        public void SetHovered(bool isHovered)
        {
            if (_isHovered == isHovered) return;
            _isHovered = isHovered;
            ApplyVisual(animate: true);
        }

        public void ApplyVisual(bool animate)
        {
            CaptureBasePos();

            bool activeState = _isPressed || _isSelected;
            if (defaultStateObject != null)
                defaultStateObject.SetActive(!activeState);

            if (pressedStateObject != null)
                pressedStateObject.SetActive(activeState);

            if (targetImage != null)
            {
                Sprite chosen = defaultSprite;
                if (_isSelected && selectedSprite != null) chosen = selectedSprite;
                else if (_isPressed && pressedSprite != null) chosen = pressedSprite;
                else if (_isHovered && hoverSprite != null) chosen = hoverSprite;

                if (chosen != null) targetImage.sprite = chosen;
            }

            if (visualRect != null && _basePosCaptured)
            {
                float liftY = _isSelected ? _selectedLiftY : (_isHovered ? _hoverLiftY : 0f);
                if (_isPressed) liftY -= 3f;
                Vector2 targetPos = _baseAnchoredPos + new Vector2(0f, liftY);

                _moveTween?.Kill();
                if (!animate)
                {
                    visualRect.anchoredPosition = targetPos;
                }
                else
                {
                    _moveTween = visualRect.DOAnchorPos(targetPos, 0.15f)
                        .SetEase(Ease.OutCubic)
                        .SetUpdate(true)
                        .SetLink(visualRect.gameObject);
                }
            }
        }

        public void Cleanup()
        {
            _moveTween?.Kill();
            _punchTween?.Kill();
            _moveTween = null;
            _punchTween = null;
        }
    }

    [SerializeField] private SpiritKeySelector _keySelector;
    [SerializeField] private SpiritSelection _selection;
    [SerializeField] private SpiritSpawner _spawner;
    [Tooltip("Urutan: [0] Spirit 1, [1] Spirit 2, [2] Spirit 3, [3] Tombol Space (All Spirits)")]
    [SerializeField] private KeyButtonSlot[] _slots = new KeyButtonSlot[4];

    private bool _eventsBound;

    private void Awake()
    {
        if (_keySelector == null)
            _keySelector = FindFirstObjectByType<SpiritKeySelector>();
        if (_selection == null)
            _selection = FindFirstObjectByType<SpiritSelection>();
        if (_spawner == null)
            _spawner = FindFirstObjectByType<SpiritSpawner>();

        BindSlotButtons();
    }

    private void OnEnable()
    {
        if (_keySelector != null)
            _keySelector.OnKeyStateChanged += HandleKeyStateChanged;
        if (_selection != null)
            _selection.OnSelectionChanged += RefreshSelectionVisuals;

        RefreshSelectionVisuals();
    }

    private void OnDisable()
    {
        if (_keySelector != null)
            _keySelector.OnKeyStateChanged -= HandleKeyStateChanged;
        if (_selection != null)
            _selection.OnSelectionChanged -= RefreshSelectionVisuals;

        if (_slots != null)
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.Cleanup();
        }
    }

    private void Start()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;
            _slots[i].CaptureBasePos();
            _slots[i].SetPressed(false);
        }
        RefreshSelectionVisuals();
    }

    private void BindSlotButtons()
    {
        if (_eventsBound || _slots == null) return;
        _eventsBound = true;

        for (int i = 0; i < _slots.Length; i++)
        {
            KeyButtonSlot slot = _slots[i];
            if (slot == null || slot.button == null) continue;

            int capturedIndex = i;
            slot.button.onClick.AddListener(() => HandleSlotClicked(capturedIndex));

            EventTrigger trigger = slot.button.GetComponent<EventTrigger>();
            if (trigger == null) trigger = slot.button.gameObject.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                slot.SetHovered(true);
                AudioManager.Instance.PlayUiHover();
            });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => slot.SetHovered(false));
            trigger.triggers.Add(exit);
        }
    }

    private void HandleSlotClicked(int slotIndex)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (_selection == null) return;

        if (slotIndex == 3)
        {
            _selection.ToggleSelectAll();
            return;
        }

        bool shiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        if (shiftHeld)
            _selection.ToggleMultiByIndex(slotIndex);
        else
            _selection.ToggleSingleByIndex(slotIndex);
    }

    private void HandleKeyStateChanged(int slotIndex, bool isPressed)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length) return;
        if (_slots[slotIndex] != null)
            _slots[slotIndex].SetPressed(isPressed);
    }

    private void RefreshSelectionVisuals()
    {
        if (_slots == null) return;

        int totalSpirits = _spawner != null ? _spawner.Spirits.Count : 0;
        int selectedCount = _selection != null ? _selection.Selected.Count : 0;

        for (int i = 0; i < 3 && i < _slots.Length; i++)
        {
            KeyButtonSlot slot = _slots[i];
            if (slot == null) continue;

            bool isSelected = false;
            if (_selection != null && _spawner != null && i < _spawner.Spirits.Count)
            {
                SpiritView spirit = _spawner.Spirits[i];
                if (spirit != null) isSelected = spirit.IsSelected;
            }
            slot.SetSelected(isSelected, animate: true);
        }

        if (_slots.Length > 3 && _slots[3] != null)
        {
            bool allSelected = totalSpirits > 0 && selectedCount >= totalSpirits;
            _slots[3].SetSelected(allSelected, animate: true);
        }
    }
}
