using UnityEngine;

public class DayNightLighting : MonoBehaviour
{
    [SerializeField] private DayNightLightingSettings _settings;
    [SerializeField] private Light _sunLight;
    [SerializeField] private Camera _camera;

    private PhaseManager _phases;
    private LightingPreset _target;
    private float _blend;

    // Snapshot of the lighting when the transition began. Starting from the
    // current values means an interrupted transition (night started early)
    // continues smoothly instead of jumping.
    private Color _fromLight;
    private float _fromIntensity;
    private Quaternion _fromRotation;
    private Color _fromAmbient;
    private Color _fromBackground;

    private void Start()
    {
        _phases = PhaseManager.Instance;

        // Flat ambient lets us drive the ambient color directly from code.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        ApplyInstant(_settings.Day);

        _phases.OnDayStart += HandleDayStart;
        _phases.OnNightStart += HandleNightStart;

        // Update only runs while a transition is active.
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
        if (_settings.TransitionDuration <= 0f)
        {
            ApplyInstant(target);
            return;
        }

        _fromLight = _sunLight.color;
        _fromIntensity = _sunLight.intensity;
        _fromRotation = _sunLight.transform.rotation;
        _fromAmbient = RenderSettings.ambientLight;
        _fromBackground = _camera != null ? _camera.backgroundColor : Color.black;

        _target = target;
        _blend = 0f;
        enabled = true;
    }

    private void Update()
    {
        _blend += Time.deltaTime / _settings.TransitionDuration;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blend));

        _sunLight.color = Color.Lerp(_fromLight, _target.LightColor, t);
        _sunLight.intensity = Mathf.Lerp(_fromIntensity, _target.LightIntensity, t);
        _sunLight.transform.rotation = Quaternion.Slerp(_fromRotation, _target.LightRotation, t);
        RenderSettings.ambientLight = Color.Lerp(_fromAmbient, _target.AmbientColor, t);
        if (_camera != null)
            _camera.backgroundColor = Color.Lerp(_fromBackground, _target.BackgroundColor, t);

        if (_blend >= 1f) enabled = false;
    }

    private void ApplyInstant(LightingPreset preset)
    {
        _sunLight.color = preset.LightColor;
        _sunLight.intensity = preset.LightIntensity;
        _sunLight.transform.rotation = preset.LightRotation;
        RenderSettings.ambientLight = preset.AmbientColor;
        if (_camera != null) _camera.backgroundColor = preset.BackgroundColor;
    }
}
