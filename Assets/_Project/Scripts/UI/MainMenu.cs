using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Main menu: Start Game, Settings pop-up, Credit pop-up, Exit.
// Only one pop-up is open at a time; while it is open the main buttons cannot take focus.
// Nothing is auto-selected on open: keyboard focus appears only after the first Arrow press.
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string _gameplayScene = SceneFlow.LevelSelectionScene;

    [Header("Main")]
    [SerializeField] private CanvasGroup _mainPanel;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _creditButton;
    [SerializeField] private Button _exitButton;

    [Header("Settings pop-up")]
    [SerializeField] private GameObject _settingsPopup;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Button _settingsCloseButton;

    [Header("Credit pop-up")]
    [SerializeField] private GameObject _creditPopup;
    [SerializeField] private Button _creditCloseButton;

    private GameObject _openPopup;
    private Selectable _focusBeforePopup;
    private UIFocusOnNavigate _focusNavigator;

    private void Awake()
    {
        if (!TryGetComponent(out _focusNavigator))
            _focusNavigator = gameObject.AddComponent<UIFocusOnNavigate>();

        _startButton.onClick.AddListener(StartGame);
        _settingsButton.onClick.AddListener(OpenSettings);
        _creditButton.onClick.AddListener(OpenCredit);
        _exitButton.onClick.AddListener(SceneFlow.Quit);
        _settingsCloseButton.onClick.AddListener(ClosePopup);
        _creditCloseButton.onClick.AddListener(ClosePopup);
        _musicSlider.onValueChanged.AddListener(SetMusicVolume);
        _sfxSlider.onValueChanged.AddListener(SetSfxVolume);

#if UNITY_WEBGL && !UNITY_EDITOR
        // A browser tab cannot be closed by the game.
        _exitButton.gameObject.SetActive(false);
#endif
        _settingsPopup.SetActive(false);
        _creditPopup.SetActive(false);
    }

    private void Start()
    {
        // The main menu can be entered from a paused result screen.
        Time.timeScale = 1f;
        _focusNavigator.SetDefaultTarget(_startButton, clearCurrentSelection: true);
    }

    private void OnDestroy()
    {
        _startButton.onClick.RemoveListener(StartGame);
        _settingsButton.onClick.RemoveListener(OpenSettings);
        _creditButton.onClick.RemoveListener(OpenCredit);
        _exitButton.onClick.RemoveListener(SceneFlow.Quit);
        _settingsCloseButton.onClick.RemoveListener(ClosePopup);
        _creditCloseButton.onClick.RemoveListener(ClosePopup);
        _musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        _sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
    }

    private void Update()
    {
        // Esc closes the open pop-up, like its Close button.
        if (_openPopup == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ClosePopup();
    }

    private void StartGame() => SceneFlow.Load(_gameplayScene);

    private void OpenSettings()
    {
        // Show saved values without writing them back.
        _musicSlider.SetValueWithoutNotify(GameAudioSettings.MusicVolume);
        _sfxSlider.SetValueWithoutNotify(GameAudioSettings.SfxVolume);
        OpenPopup(_settingsPopup, _musicSlider, _settingsButton);
        if (_musicSlider.TryGetComponent(out UIStateSlider musicVisual)) musicVisual.Refresh();
        if (_sfxSlider.TryGetComponent(out UIStateSlider sfxVisual)) sfxVisual.Refresh();
    }

    private void OpenCredit() => OpenPopup(_creditPopup, _creditCloseButton, _creditButton);

    private void OpenPopup(GameObject popup, Selectable firstControl, Selectable returnTarget)
    {
        if (_openPopup != null) ClosePopup();
        _openPopup = popup;
        _focusBeforePopup = returnTarget;
        popup.SetActive(true);
        // Trap keyboard/gamepad navigation inside the pop-up without auto-highlighting anything.
        _mainPanel.interactable = false;
        _focusNavigator.SetDefaultTarget(firstControl, clearCurrentSelection: true);
    }

    private void ClosePopup()
    {
        if (_openPopup == null) return;
        if (_openPopup == _settingsPopup) GameAudioSettings.Save();
        _openPopup.SetActive(false);
        _openPopup = null;
        _mainPanel.interactable = true;
        _focusNavigator.SetDefaultTarget(_focusBeforePopup != null ? _focusBeforePopup : _startButton,
            clearCurrentSelection: true);
    }

    private static void SetMusicVolume(float value) => GameAudioSettings.MusicVolume = value;
    private static void SetSfxVolume(float value) => GameAudioSettings.SfxVolume = value;
}
