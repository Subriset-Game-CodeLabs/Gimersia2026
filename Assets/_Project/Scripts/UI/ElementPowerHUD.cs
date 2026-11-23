using System;
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

        [Header("2A. Fill Grid (Kotak-Kotak Segmen Terpisah)")]
        [Tooltip("Masukkan daftar Image kotak grid secara berurutan dari kiri ke kanan / bawah ke atas.")]
        [SerializeField] private Image[] _gridCells;
        [Tooltip("Jumlah power untuk menyalakan 1 kotak grid (default 10, sesuai kelipatan buff stat).")]
        [SerializeField, Min(1)] private int _powerPerCell = 10;
        [SerializeField] private Color _activeCellColor = Color.white;
        [SerializeField] private Color _inactiveCellColor = new Color(1f, 1f, 1f, 0.18f);

        [Header("2B. Opsional: Single Image Fill (Jika memakai 1 Image bertekstur grid)")]
        [SerializeField] private Image _singleFillGridImage;
        [SerializeField, Min(10)] private int _maxBarPower = 100;

        private int _lastDisplayedPower = -1;
        private float _targetFill01;
        private float _currentFill01;
        private float _textPunchTimer;
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

        public void SetPower(int totalPower, bool instant)
        {
            Initialize();

            // 1. Update Angka Power
            if (_powerText != null && _lastDisplayedPower != totalPower)
            {
                _powerText.text = string.Format(_numberFormat, totalPower);
                if (!instant && _lastDisplayedPower >= 0)
                {
                    _textPunchTimer = 0.22f;
                }
            }
            _lastDisplayedPower = totalPower;

            // 2A. Update Kotak-Kotak Grid Segmen (1 kotak = +10 Power)
            if (_gridCells != null && _gridCells.Length > 0)
            {
                int litCount = Mathf.Clamp(totalPower / Mathf.Max(1, _powerPerCell), 0, _gridCells.Length);
                for (int i = 0; i < _gridCells.Length; i++)
                {
                    if (_gridCells[i] == null) continue;
                    _gridCells[i].color = (i < litCount) ? _activeCellColor : _inactiveCellColor;
                }
            }

            // 2B. Update Single Fill Image (jika digunakan)
            _targetFill01 = Mathf.Clamp01((float)totalPower / Mathf.Max(1, _maxBarPower));
            if (instant)
            {
                _currentFill01 = _targetFill01;
                if (_singleFillGridImage != null)
                    _singleFillGridImage.fillAmount = _currentFill01;
            }
        }

        public void TickVisual(float deltaTime, float fillLerpSpeed)
        {
            if (_singleFillGridImage != null && !Mathf.Approximately(_currentFill01, _targetFill01))
            {
                _currentFill01 = Mathf.MoveTowards(_currentFill01, _targetFill01, fillLerpSpeed * deltaTime);
                _singleFillGridImage.fillAmount = _currentFill01;
            }

            if (_powerText != null && _textPunchTimer > 0f)
            {
                _textPunchTimer = Mathf.Max(0f, _textPunchTimer - deltaTime);
                float t = 1f - (_textPunchTimer / 0.22f);
                float punch = 1f + Mathf.Sin(t * Mathf.PI) * 0.22f;
                _powerText.transform.localScale = _baseTextScale * punch;
            }
        }
    }

    [Header("Konfigurasi 3 Elemen (Api, Air, Petir)")]
    [SerializeField] private ElementPowerSlot[] _elementSlots =
    {
        new ElementPowerSlot { label = "Api (Fire)", element = ElementType.Fire },
        new ElementPowerSlot { label = "Air (Water)", element = ElementType.Water },
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
            slot.SetPower(power, instant);
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