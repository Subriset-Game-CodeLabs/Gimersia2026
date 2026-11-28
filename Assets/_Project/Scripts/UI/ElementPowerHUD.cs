using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ElementPowerHUD : MonoBehaviour
{
    [Serializable]
    public class ElementPowerSlot
    {
        public string label;
        public ElementType element = ElementType.Fire;

        [Header("1. Tampilan Angka Power")]
        [SerializeField] private TMP_Text _powerText;
        [SerializeField] private string _numberFormat = "{0}";
        [Tooltip("Jika aktif, tampilkan format '{power} +{bonus}% ATK' sesuai ElementSettings.")]
        [SerializeField] private bool _showAttackBonus = true;

        [Header("2A. Fill Grid (Kotak-Kotak Segmen Terpisah)")]
        [Tooltip("Masukkan daftar Image kotak grid secara berurutan dari kiri ke kanan / bawah ke atas.")]
        [SerializeField] private Image[] _gridCells;
        [Tooltip("Jumlah power untuk menyalakan 1 kotak grid (default 10, sesuai kelipatan buff stat).")]
        [SerializeField, Min(1)] private int _powerPerCell = 10;
        [SerializeField] private Color _activeCellColor = Color.white;
        [SerializeField] private Color _inactiveCellColor = new Color(1f, 1f, 1f, 0.18f);

        [Header("2B. Opsional: Single Image Fill / Anchor Fill (UI v2 Capsule Bar)")]
        [SerializeField] private Image _singleFillGridImage;
        [SerializeField] private RectTransform _anchorFillRect;
        [SerializeField, Range(0f, 0.4f)] private float _minNonZeroFill = 0.18f;
        [SerializeField, Min(10)] private int _maxBarPower = 100;

        private int _lastDisplayedPower = -1;
        private float _targetFill01;
        private float _currentFill01;
        private Tween _punchTween;
        private Vector3 _baseTextScale = Vector3.one;
        private bool _cachedScale;

        public void Initialize()
        {
            if (_powerText != null && !_cachedScale)
            {
                _baseTextScale = _powerText.transform.localScale;
                _cachedScale = true;
            }
        }

        public void SetPower(int totalPower, ElementSettings settings, bool instant)
        {
            Initialize();

            // 1. Update Angka Power (+ Bonus ATK% dinamis)
            if (_powerText != null && _lastDisplayedPower != totalPower)
            {
                if (_showAttackBonus)
                {
                    int bonusPct = 0;
                    if (settings != null)
                    {
                        float mult = settings.GetDamageBonusMultiplier(totalPower);
                        bonusPct = Mathf.Max(0, Mathf.RoundToInt((mult - 1f) * 100f));
                    }
                    _powerText.text = $"{totalPower} +{bonusPct}% ATK";
                }
                else
                {
                    _powerText.text = string.Format(_numberFormat, totalPower);
                }

                if (!instant && _lastDisplayedPower >= 0)
                {
                    _punchTween?.Kill();
                    _powerText.transform.localScale = _baseTextScale;
                    _punchTween = _powerText.transform.DOPunchScale(_baseTextScale * 0.14f, 0.22f, 4, 0.5f)
                        .SetUpdate(true)
                        .SetLink(_powerText.gameObject);
                }
            }
            _lastDisplayedPower = totalPower;

            // 2A. Update Kotak-Kotak Grid Segmen (legacy support)
            if (_gridCells != null && _gridCells.Length > 0)
            {
                int litCount = Mathf.Clamp(totalPower / Mathf.Max(1, _powerPerCell), 0, _gridCells.Length);
                for (int i = 0; i < _gridCells.Length; i++)
                {
                    if (_gridCells[i] == null) continue;
                    _gridCells[i].color = (i < litCount) ? _activeCellColor : _inactiveCellColor;
                }
            }

            // 2B. Update Smooth Bar Fill
            float rawFill = Mathf.Clamp01((float)totalPower / Mathf.Max(1, _maxBarPower));
            _targetFill01 = totalPower > 0 ? Mathf.Max(_minNonZeroFill, rawFill) : 0f;

            if (instant)
            {
                _currentFill01 = _targetFill01;
                ApplyFill(_currentFill01);
            }
        }

        public void TickVisual(float deltaTime, float fillLerpSpeed)
        {
            if ((_singleFillGridImage != null || _anchorFillRect != null) && !Mathf.Approximately(_currentFill01, _targetFill01))
            {
                _currentFill01 = Mathf.MoveTowards(_currentFill01, _targetFill01, fillLerpSpeed * deltaTime);
                ApplyFill(_currentFill01);
            }
        }

        public void Cleanup()
        {
            _punchTween?.Kill();
            _punchTween = null;
        }

        private void ApplyFill(float fill01)
        {
            if (_singleFillGridImage != null)
                _singleFillGridImage.fillAmount = fill01;

            if (_anchorFillRect != null)
            {
                _anchorFillRect.anchorMin = new Vector2(0f, 0f);
                _anchorFillRect.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
                _anchorFillRect.offsetMin = Vector2.zero;
                _anchorFillRect.offsetMax = Vector2.zero;
                _anchorFillRect.gameObject.SetActive(fill01 > 0.001f);
            }
        }
    }

    [Header("Konfigurasi 3 Elemen (Air, Api, Petir)")]
    [SerializeField] private ElementPowerSlot[] _elementSlots =
    {
        new ElementPowerSlot { label = "Air (Water)", element = ElementType.Water },
        new ElementPowerSlot { label = "Api (Fire)", element = ElementType.Fire },
        new ElementPowerSlot { label = "Petir (Lightning)", element = ElementType.Lightning }
    };

    [SerializeField] private float _fillSmoothSpeed = 3.5f;

    private TreeRegistry _subscribedRegistry;

    private void OnEnable()
    {
        TrySubscribe();
        RefreshAll(instant: true);
    }

    private void Start()
    {
        TrySubscribe();
        RefreshAll(instant: true);
    }

    private void OnDisable()
    {
        if (_subscribedRegistry != null)
        {
            _subscribedRegistry.OnPowerChanged -= HandlePowerChanged;
            _subscribedRegistry = null;
        }

        if (_elementSlots != null)
        {
            for (int i = 0; i < _elementSlots.Length; i++)
                _elementSlots[i]?.Cleanup();
        }
    }

    private void TrySubscribe()
    {
        TreeRegistry registry = TreeRegistry.Instance;
        if (registry == null || _subscribedRegistry == registry) return;

        if (_subscribedRegistry != null)
            _subscribedRegistry.OnPowerChanged -= HandlePowerChanged;

        _subscribedRegistry = registry;
        _subscribedRegistry.OnPowerChanged += HandlePowerChanged;
    }

    private void HandlePowerChanged()
    {
        RefreshAll(instant: false);
    }

    public void RefreshAll(bool instant)
    {
        TreeRegistry registry = TreeRegistry.Instance;
        if (registry == null) return;

        for (int i = 0; i < _elementSlots.Length; i++)
        {
            ElementPowerSlot slot = _elementSlots[i];
            if (slot == null) continue;

            int power = registry.GetTotalPower(slot.element);
            slot.SetPower(power, registry.Settings, instant);
        }
    }

    private void Update()
    {
        if (_subscribedRegistry == null && TreeRegistry.Instance != null)
        {
            TrySubscribe();
            RefreshAll(instant: true);
        }

        for (int i = 0; i < _elementSlots.Length; i++)
        {
            _elementSlots[i]?.TickVisual(Time.deltaTime, _fillSmoothSpeed);
        }
    }
}
