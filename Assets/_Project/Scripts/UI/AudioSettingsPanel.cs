using System;
using UnityEngine;
using UnityEngine.UI;

// Attach to the settings panel itself. The owning menu controls visibility and navigation.
[DisallowMultipleComponent]
public sealed class AudioSettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Button _backButton;
    private bool _bound;
    private bool _interactable = true;

    public event Action OnCloseRequested;
    public Selectable FirstControl => _musicSlider;
    public Button BackButton => _backButton;

    // Preserve existing scene and MenuFlowBuilder bindings without modifying scene assets.
    public static AudioSettingsPanel Resolve(GameObject panel, Slider legacyMusic, Slider legacySfx, Button legacyBack)
    {
        if (panel == null) return null;
        if (panel.TryGetComponent(out AudioSettingsPanel existing)) return existing;
        if (legacyMusic == null || legacySfx == null || legacyBack == null) return null;

        AudioSettingsPanel created = panel.AddComponent<AudioSettingsPanel>();
        created._musicSlider = legacyMusic;
        created._sfxSlider = legacySfx;
        created._backButton = legacyBack;
        if (created.isActiveAndEnabled) created.BindControls();
        return created;
    }

    private void OnEnable() => BindControls();

    private void OnDisable()
    {
        if (!_bound) return;
        _musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        _sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
        _backButton.onClick.RemoveListener(RequestClose);
        _bound = false;
        GameAudioSettings.Save();
    }

    private void BindControls()
    {
        if (_bound || !TryValidate(out _)) return;
        _musicSlider.onValueChanged.AddListener(SetMusicVolume);
        _sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        _backButton.onClick.AddListener(RequestClose);
        _bound = true;
        UIButtonAudio.Attach(_backButton, UIButtonAudio.ClickSoundType.None);
        RefreshValues();
        SetInteractable(_interactable);
    }

    public void SetInteractable(bool interactable)
    {
        _interactable = interactable;
        if (_musicSlider != null) _musicSlider.interactable = interactable;
        if (_sfxSlider != null) _sfxSlider.interactable = interactable;
        if (_backButton != null) _backButton.interactable = interactable;
    }

    private void SetMusicVolume(float value)
    {
        if (_interactable && isActiveAndEnabled) GameAudioSettings.MusicVolume = value;
    }

    private void SetSfxVolume(float value)
    {
        if (!_interactable || !isActiveAndEnabled) return;
        GameAudioSettings.SfxVolume = value;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayUiSliderTick();
    }

    private void RequestClose()
    {
        if (_interactable && isActiveAndEnabled) OnCloseRequested?.Invoke();
    }

    public bool TryValidate(out string error)
    {
        error = null;
        if (!enabled || _musicSlider == null || _sfxSlider == null || _backButton == null
            || _musicSlider == _sfxSlider
            || !_musicSlider.transform.IsChildOf(transform)
            || !_sfxSlider.transform.IsChildOf(transform)
            || !_backButton.transform.IsChildOf(transform)
            || _musicSlider.minValue != 0f || _musicSlider.maxValue != 1f || _musicSlider.wholeNumbers
            || _sfxSlider.minValue != 0f || _sfxSlider.maxValue != 1f || _sfxSlider.wholeNumbers)
        {
            error = "Assign two distinct continuous 0..1 volume sliders and a Back button inside an enabled AudioSettingsPanel.";
            return false;
        }
        return true;
    }

    public void RefreshValues()
    {
        if (!TryValidate(out _)) return;
        _musicSlider.SetValueWithoutNotify(GameAudioSettings.MusicVolume);
        _sfxSlider.SetValueWithoutNotify(GameAudioSettings.SfxVolume);
        if (_musicSlider.TryGetComponent(out UIStateSlider musicVisual)) musicVisual.Refresh();
        if (_sfxSlider.TryGetComponent(out UIStateSlider sfxVisual)) sfxVisual.Refresh();
    }
}
