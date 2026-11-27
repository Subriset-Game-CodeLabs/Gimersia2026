using UnityEngine;
using UnityEngine.Rendering;

public class DayNightLighting : MonoBehaviour
{
    [SerializeField] private DayNightLightingSettings _settings;
    [SerializeField] private Light _sunLight;
    [SerializeField] private Camera _camera;

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

        if (_blend >= 1f) enabled = false;
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
    }
}