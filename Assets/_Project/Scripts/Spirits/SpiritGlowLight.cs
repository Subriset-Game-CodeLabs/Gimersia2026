using UnityEngine;

public class SpiritGlowLight : MonoBehaviour
{
    [Header("Point Light (Otomatis Dibuat Jika Kosong)")]
    [SerializeField] private Light _pointLight;
    [SerializeField] private float _heightOffset = 0.85f;
    [SerializeField, Min(0.5f)] private float _lightRange = 2.6f;

    [Header("Intensitas Siang vs Malam")]
    [SerializeField, Min(0f)] private float _nightIntensity = 1.05f;
    [SerializeField, Min(0f)] private float _dayIntensity = 0.12f;
    [SerializeField, Min(1f)] private float _selectedBoostMultiplier = 1.3f;
    [SerializeField, Min(0.05f)] private float _fadeDuration = 1.8f;

    [Header("Warna Cahaya per Elemen")]
    [SerializeField] private Color _fireColor = new Color(1f, 0.52f, 0.22f, 1f);
    [SerializeField] private Color _waterColor = new Color(0.28f, 0.86f, 1f, 1f);
    [SerializeField] private Color _lightningColor = new Color(1f, 0.86f, 0.30f, 1f);
    [SerializeField] private Color _neutralColor = new Color(0.75f, 0.95f, 0.85f, 1f);

    private SpiritView _spiritView;
    private PhaseManager _phases;
    private bool _isNight;
    private float _currentIntensity;

    private void Awake()
    {
        _spiritView = GetComponent<SpiritView>();
        EnsureLight();
    }

    private void OnEnable()
    {
        _phases = PhaseManager.Instance;
        if (_phases != null)
        {
            _isNight = _phases.CurrentPhase == PhaseType.Night;
            _phases.OnDayStart += HandleDayStart;
            _phases.OnNightStart += HandleNightStart;
        }

        ApplyColorFromElement();
        _currentIntensity = GetDesiredIntensity();
        ApplyIntensity(_currentIntensity);
    }

    private void OnDisable()
    {
        if (_phases != null)
        {
            _phases.OnDayStart -= HandleDayStart;
            _phases.OnNightStart -= HandleNightStart;
            _phases = null;
        }
    }

    private void HandleDayStart(int day) => _isNight = false;
    private void HandleNightStart(int night) => _isNight = true;

    private void EnsureLight()
    {
        if (_pointLight == null)
        {
            GameObject lightObj = new GameObject("SpiritGlowLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, _heightOffset, 0f);
            _pointLight = lightObj.AddComponent<Light>();
        }

        _pointLight.type = LightType.Point;
        _pointLight.shadows = LightShadows.None;
        _pointLight.range = _lightRange;
    }

    private void ApplyColorFromElement()
    {
        if (_pointLight == null) return;
        ElementType element = _spiritView != null ? _spiritView.Element : ElementType.None;

        switch (element)
        {
            case ElementType.Fire:
                _pointLight.color = _fireColor;
                break;
            case ElementType.Water:
                _pointLight.color = _waterColor;
                break;
            case ElementType.Lightning:
                _pointLight.color = _lightningColor;
                break;
            default:
                _pointLight.color = _neutralColor;
                break;
        }
    }

    private float GetDesiredIntensity()
    {
        float baseVal = _isNight ? _nightIntensity : _dayIntensity;
        if (_spiritView != null && _spiritView.IsSelected)
            baseVal *= _selectedBoostMultiplier;
        return baseVal;
    }

    private void Update()
    {
        if (_pointLight == null) return;

        float desired = GetDesiredIntensity();
        float speed = (_fadeDuration > 0.01f) ? (Mathf.Max(_nightIntensity, _dayIntensity) / _fadeDuration) : 50f;
        _currentIntensity = Mathf.MoveTowards(_currentIntensity, desired, speed * Time.deltaTime);
        ApplyIntensity(_currentIntensity);
    }

    private void ApplyIntensity(float value)
    {
        _pointLight.intensity = value;
        _pointLight.enabled = value > 0.001f;
    }
}