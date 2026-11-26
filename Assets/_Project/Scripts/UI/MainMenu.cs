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
    // Preserve existing scenes and MenuFlowBuilder output while reusing the shared component.
    [SerializeField, HideInInspector] private Slider _musicSlider;
    [SerializeField, HideInInspector] private Slider _sfxSlider;
    [SerializeField, HideInInspector] private Button _settingsCloseButton;
    private AudioSettingsPanel _audioSettings;

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

        _audioSettings = AudioSettingsPanel.Resolve(_settingsPopup, _musicSlider, _sfxSlider, _settingsCloseButton);
        if (_audioSettings != null) _audioSettings.OnCloseRequested += ClosePopup;
        _startButton.onClick.AddListener(StartGame);
        _settingsButton.onClick.AddListener(OpenSettings);
        _creditButton.onClick.AddListener(OpenCredit);
        _exitButton.onClick.AddListener(SceneFlow.Quit);
        _creditCloseButton.onClick.AddListener(ClosePopup);

        // Pasang SFX hover & klik otomatis ke seluruh tombol Main Menu
        UIButtonAudio.Attach(_startButton, UIButtonAudio.ClickSoundType.StartGame);
        UIButtonAudio.Attach(_settingsButton, UIButtonAudio.ClickSoundType.StandardClick);
        UIButtonAudio.Attach(_creditButton, UIButtonAudio.ClickSoundType.StandardClick);
        UIButtonAudio.Attach(_exitButton, UIButtonAudio.ClickSoundType.StandardClick);
        UIButtonAudio.Attach(_creditCloseButton, UIButtonAudio.ClickSoundType.None);

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
        AudioManager.Instance.PlayMenuMusic();
        _focusNavigator.SetDefaultTarget(_startButton, clearCurrentSelection: true);
    }

    private void OnDestroy()
    {
        _startButton.onClick.RemoveListener(StartGame);
        _settingsButton.onClick.RemoveListener(OpenSettings);
        _creditButton.onClick.RemoveListener(OpenCredit);
        _exitButton.onClick.RemoveListener(SceneFlow.Quit);
        if (_audioSettings != null) _audioSettings.OnCloseRequested -= ClosePopup;
        _creditCloseButton.onClick.RemoveListener(ClosePopup);
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
        if (_audioSettings == null)
        {
            Debug.LogError("MainMenu: add AudioSettingsPanel to the settings pop-up and assign its controls.", this);
            return;
        }
        if (!_audioSettings.TryValidate(out string error))
        {
            Debug.LogError("MainMenu: " + error, this);
            return;
        }
        OpenPopup(_settingsPopup, _audioSettings.FirstControl, _settingsButton);
    }

    private void OpenCredit() => OpenPopup(_creditPopup, _creditCloseButton, _creditButton);

    private void OpenPopup(GameObject popup, Selectable firstControl, Selectable returnTarget)
    {
        if (_openPopup != null) ClosePopup();
        _openPopup = popup;
        _focusBeforePopup = returnTarget;
        popup.SetActive(true);
        AudioManager.Instance.PlayUiPopupOpen();
        // Trap keyboard/gamepad navigation inside the pop-up without auto-highlighting anything.
        _mainPanel.interactable = false;
        _focusNavigator.SetDefaultTarget(firstControl, clearCurrentSelection: true);
    }

    private void ClosePopup()
    {
        if (_openPopup == null) return;
        AudioManager.Instance.PlayUiPopupClose();
        _openPopup.SetActive(false);
        _openPopup = null;
        _mainPanel.interactable = true;
        _focusNavigator.SetDefaultTarget(_focusBeforePopup != null ? _focusBeforePopup : _startButton,
            clearCurrentSelection: true);
    }

}