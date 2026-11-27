using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DayNightLighting : MonoBehaviour
{
    [SerializeField] private DayNightLightingSettings _settings;
    [SerializeField] private Light _sunLight;
    [SerializeField] private Camera _camera;

    [Header("Daytime VFX")]
    [Tooltip("Daftar objek VFX (parent prefab atau child ParticleSystem) yang hanya aktif saat siang hari.")]
    [SerializeField] private List<GameObject> _dayVfxObjects = new List<GameObject>();

    private struct CachedParticleSystem
    {
        public ParticleSystem system;
        public float baseRateMultiplier;
    }

    private readonly List<CachedParticleSystem> _cachedVfxSystems = new List<CachedParticleSystem>();
    private ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[128];
    private bool _isDayVfxActive;

    private PhaseManager _phases;
    private LightingPreset _target;
    private float _blend;

    private Color _fromLight;
    private float _fromIntensity;
    private Quaternion _fromRotation;
    private float _fromShadowStrength;
    private Color _fromSkyAmbient;
    private Color _fromEquatorAmbient;
    private Color _fromGroundAmbient;
    private Color _fromBackground;

    private void Start()
    {
        if (_settings == null)
        {
            Debug.LogError("DayNightLighting: DayNightLightingSettings belum di-assign.", this);
            enabled = false;
            return;
        }

        if (_camera == null) _camera = Camera.main;
        _phases = PhaseManager.Instance;

        CacheDayVfx();

        // Trilight = Gradient Ambient (Sky, Equator, Ground)
        RenderSettings.ambientMode = AmbientMode.Trilight;
        ApplyInstant(_settings.Day);

        if (_phases != null)
        {
            _phases.OnDayStart += HandleDayStart;
            _phases.OnNightStart += HandleNightStart;
        }

        enabled = false;
    }

    private void OnDestroy()
    {
        if (_phases == null) return;
        _phases.OnDayStart -= HandleDayStart;
        _phases.OnNightStart -= HandleNightStart;
    }

    private void HandleDayStart(int day) => BeginTransition(_settings.Day);
    private void HandleNightStart(int night) => BeginTransition(_settings.Night);

    private void BeginTransition(LightingPreset target)
    {
        if (target == null) return;

        if (_settings.TransitionDuration <= 0f)
        {
            ApplyInstant(target);
            return;
        }

        if (_sunLight != null)
        {
            _fromLight = _sunLight.color;
            _fromIntensity = _sunLight.intensity;
            _fromRotation = _sunLight.transform.rotation;
            _fromShadowStrength = _sunLight.shadowStrength;
        }

        _fromSkyAmbient = RenderSettings.ambientSkyColor;
        _fromEquatorAmbient = RenderSettings.ambientEquatorColor;
        _fromGroundAmbient = RenderSettings.ambientGroundColor;
        _fromBackground = _camera != null ? _camera.backgroundColor : Color.black;

        _target = target;
        _blend = 0f;

        if (_target == _settings.Day)
        {
            ActivateDayVfxForTransition();
        }
        else if (_target == _settings.Night)
        {
            _isDayVfxActive = false;
        }

        enabled = true;
    }

    private void Update()
    {
        _blend += Time.deltaTime / _settings.TransitionDuration;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blend));

        if (_sunLight != null)
        {
            _sunLight.color = Color.Lerp(_fromLight, _target.LightColor, t);
            _sunLight.intensity = Mathf.Lerp(_fromIntensity, _target.LightIntensity, t);
            _sunLight.transform.rotation = Quaternion.Slerp(_fromRotation, _target.LightRotation, t);
            _sunLight.shadowStrength = Mathf.Lerp(_fromShadowStrength, _target.ShadowStrength, t);
        }

        RenderSettings.ambientSkyColor = Color.Lerp(_fromSkyAmbient, _target.AmbientSkyColor, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(_fromEquatorAmbient, _target.AmbientEquatorColor, t);
        RenderSettings.ambientGroundColor = Color.Lerp(_fromGroundAmbient, _target.AmbientGroundColor, t);

        if (_camera != null)
            _camera.backgroundColor = Color.Lerp(_fromBackground, _target.BackgroundColor, t);

        UpdateDayVfxTransition(t);

        if (_blend >= 1f)
        {
            if (_target == _settings.Night)
            {
                SetDayVfxInstant(false);
            }
            enabled = false;
        }
    }

    private void ApplyInstant(LightingPreset preset)
    {
        if (preset == null) return;

        if (_sunLight != null)
        {
            _sunLight.color = preset.LightColor;
            _sunLight.intensity = preset.LightIntensity;
            _sunLight.transform.rotation = preset.LightRotation;
            _sunLight.shadowStrength = preset.ShadowStrength;
        }

        RenderSettings.ambientSkyColor = preset.AmbientSkyColor;
        RenderSettings.ambientEquatorColor = preset.AmbientEquatorColor;
        RenderSettings.ambientGroundColor = preset.AmbientGroundColor;

        if (_camera != null)
            _camera.backgroundColor = preset.BackgroundColor;

        SetDayVfxInstant(preset == _settings.Day);
    }

    private void CacheDayVfx()
    {
        _cachedVfxSystems.Clear();
        int maxCapacity = 128;

        for (int i = 0; i < _dayVfxObjects.Count; i++)
        {
            GameObject vfxObj = _dayVfxObjects[i];
            if (vfxObj == null) continue;

            ParticleSystem[] systems = vfxObj.GetComponentsInChildren<ParticleSystem>(true);
            for (int j = 0; j < systems.Length; j++)
            {
                ParticleSystem ps = systems[j];
                if (ps == null) continue;

                _cachedVfxSystems.Add(new CachedParticleSystem
                {
                    system = ps,
                    baseRateMultiplier = ps.emission.rateOverTimeMultiplier
                });

                if (ps.main.maxParticles > maxCapacity)
                    maxCapacity = ps.main.maxParticles;
            }
        }

        if (_particleBuffer.Length < maxCapacity)
            _particleBuffer = new ParticleSystem.Particle[maxCapacity];
    }

    private void ActivateDayVfxForTransition()
    {
        for (int i = 0; i < _dayVfxObjects.Count; i++)
        {
            if (_dayVfxObjects[i] != null && !_dayVfxObjects[i].activeSelf)
                _dayVfxObjects[i].SetActive(true);
        }

        for (int i = 0; i < _cachedVfxSystems.Count; i++)
        {
            ParticleSystem ps = _cachedVfxSystems[i].system;
            if (ps == null) continue;

            if (!_isDayVfxActive)
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier = 0f;
            }

            if (!ps.isPlaying)
                ps.Play(true);
        }
    }

    private void UpdateDayVfxTransition(float t)
    {
        if (_target == _settings.Day)
        {
            if (_isDayVfxActive) return;

            for (int i = 0; i < _cachedVfxSystems.Count; i++)
            {
                CachedParticleSystem entry = _cachedVfxSystems[i];
                if (entry.system == null) continue;

                var emission = entry.system.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(0f, entry.baseRateMultiplier, t);
            }

            if (t >= 1f) _isDayVfxActive = true;
        }
        else if (_target == _settings.Night)
        {
            float remaining = 1f - t;
            byte maxAlpha = (byte)Mathf.RoundToInt(255f * remaining);

            for (int i = 0; i < _cachedVfxSystems.Count; i++)
            {
                CachedParticleSystem entry = _cachedVfxSystems[i];
                ParticleSystem ps = entry.system;
                if (ps == null) continue;

                var emission = ps.emission;
                emission.rateOverTimeMultiplier = entry.baseRateMultiplier * remaining;

                int count = ps.GetParticles(_particleBuffer);
                for (int p = 0; p < count; p++)
                {
                    Color32 color = _particleBuffer[p].startColor;
                    if (color.a > maxAlpha)
                    {
                        color.a = maxAlpha;
                        _particleBuffer[p].startColor = color;
                    }
                }
                ps.SetParticles(_particleBuffer, count);
            }
        }
    }

    private void SetDayVfxInstant(bool active)
    {
        _isDayVfxActive = active;

        if (active)
        {
            for (int i = 0; i < _dayVfxObjects.Count; i++)
            {
                if (_dayVfxObjects[i] != null)
                    _dayVfxObjects[i].SetActive(true);
            }

            for (int i = 0; i < _cachedVfxSystems.Count; i++)
            {
                CachedParticleSystem entry = _cachedVfxSystems[i];
                if (entry.system == null) continue;

                var emission = entry.system.emission;
                emission.rateOverTimeMultiplier = entry.baseRateMultiplier;
                entry.system.Play(true);
            }
        }
        else
        {
            for (int i = 0; i < _cachedVfxSystems.Count; i++)
            {
                ParticleSystem ps = _cachedVfxSystems[i].system;
                if (ps == null) continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            for (int i = 0; i < _dayVfxObjects.Count; i++)
            {
                if (_dayVfxObjects[i] != null)
                    _dayVfxObjects[i].SetActive(false);
            }
        }
    }
}