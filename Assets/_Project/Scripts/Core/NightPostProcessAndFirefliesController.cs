using UnityEngine;
using UnityEngine.Rendering;

public class NightPostProcessAndFirefliesController : MonoBehaviour
{
    [Header("1. Post-Processing Volume Malam")]
    [Tooltip("Global Volume dengan Priority lebih tinggi yang aktif saat malam.")]
    [SerializeField] private Volume _nightVolume;

    [Header("2. Partikel Kunang-Kunang (Fireflies)")]
    [SerializeField] private ParticleSystem _firefliesParticles;
    [SerializeField, Min(0f)] private float _dayEmissionRate = 0f;
    [SerializeField, Min(0f)] private float _nightEmissionRate = 14f;

    [Header("3. Durasi Transisi")]
    [SerializeField, Min(0f)] private float _transitionDuration = 2.2f;

    private PhaseManager _phases;
    private ParticleSystem.EmissionModule _fireflyEmission;
    private bool _hasFireflies;

    private float _blend = 1f;
    private bool _targetIsNight;
    private float _fromWeight;
    private float _fromEmissionRate;

    private void Start()
    {
        if (_firefliesParticles != null)
        {
            _fireflyEmission = _firefliesParticles.emission;
            _hasFireflies = true;
            if (!_firefliesParticles.isPlaying)
                _firefliesParticles.Play();
        }

        _phases = PhaseManager.Instance;
        bool startNight = _phases != null && _phases.CurrentPhase == PhaseType.Night;
        ApplyInstant(startNight);

        if (_phases != null)
        {
            _phases.OnDayStart += HandleDayStart;
            _phases.OnNightStart += HandleNightStart;
        }

        enabled = false;
    }

    private void OnDestroy()
    {
        if (_phases != null)
        {
            _phases.OnDayStart -= HandleDayStart;
            _phases.OnNightStart -= HandleNightStart;
        }
    }

    private void HandleDayStart(int day) => BeginTransition(isNight: false);
    private void HandleNightStart(int night) => BeginTransition(isNight: true);

    private void BeginTransition(bool isNight)
    {
        if (_transitionDuration <= 0f)
        {
            ApplyInstant(isNight);
            return;
        }

        _targetIsNight = isNight;
        _blend = 0f;
        _fromWeight = _nightVolume != null ? _nightVolume.weight : (isNight ? 0f : 1f);
        _fromEmissionRate = _hasFireflies ? _fireflyEmission.rateOverTime.constant : 0f;
        enabled = true;
    }

    private void Update()
    {
        _blend += Time.deltaTime / _transitionDuration;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blend));

        if (_nightVolume != null)
        {
            float targetWeight = _targetIsNight ? 1f : 0f;
            _nightVolume.weight = Mathf.Lerp(_fromWeight, targetWeight, t);
        }

        if (_hasFireflies)
        {
            float targetRate = _targetIsNight ? _nightEmissionRate : _dayEmissionRate;
            _fireflyEmission.rateOverTime = Mathf.Lerp(_fromEmissionRate, targetRate, t);
        }

        if (_blend >= 1f)
            enabled = false;
    }

    private void ApplyInstant(bool isNight)
    {
        _targetIsNight = isNight;

        if (_nightVolume != null)
            _nightVolume.weight = isNight ? 1f : 0f;

        if (_hasFireflies)
            _fireflyEmission.rateOverTime = isNight ? _nightEmissionRate : _dayEmissionRate;
    }
}