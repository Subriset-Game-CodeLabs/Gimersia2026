using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Place on an always-active prefab root, outside the overlay it hides.
[DisallowMultipleComponent]
public sealed class PauseMenu : MonoBehaviour
{
    [Header("Prefab UI")]
    [SerializeField] private GameObject _overlay;
    [SerializeField] private GameObject _pausePanel;
    [Tooltip("Optional HUD button. Escape and P also control the pause menu.")]
    [SerializeField] private Button _pauseButton;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _mainMenuButton;

    [Header("Settings (sibling of Pause Panel inside Overlay)")]
    [SerializeField] private GameObject _settingsPanel;
    // Retained for existing serialized assignments; new prefabs use AudioSettingsPanel.
    [SerializeField, HideInInspector] private Slider _musicSlider;
    [SerializeField, HideInInspector] private Slider _sfxSlider;
    [SerializeField, HideInInspector] private Button _settingsBackButton;
    private AudioSettingsPanel _audioSettings;

    private PhaseManager _phases;
    private WinLoseChecker _results;
    private BagHUD _bag;
    private ItemUseController _itemUse;
    private TreeElementMenu _treeMenu;
    private UIFocusOnNavigate _focusNavigator;
    private InputAction _escape;
    private bool _escapePending;
    private bool _ready;
    private float _previousTimeScale;
    private bool _settingsOpen;

    public bool IsPaused { get; private set; }

    private bool IsPlayable => _phases != null && _results != null
        && _results.Result == RunResult.Playing
        && (_phases.CurrentPhase == PhaseType.Day || _phases.CurrentPhase == PhaseType.Night);

    private void Awake()
    {
        _escape = new InputAction("PauseMenu", InputActionType.Button);
        _escape.AddBinding("<Keyboard>/escape");
        _escape.AddBinding("<Keyboard>/p");
        if (!TryGetComponent(out _focusNavigator))
            _focusNavigator = gameObject.AddComponent<UIFocusOnNavigate>();
        if (HasSeparateOverlay()) _overlay.SetActive(false);
    }

    private void OnEnable()
    {
        if (_pauseButton != null) _pauseButton.onClick.AddListener(Pause);
        if (_resumeButton != null) _resumeButton.onClick.AddListener(Resume);
        if (_restartButton != null) _restartButton.onClick.AddListener(Restart);
        if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(MainMenu);
        if (_settingsButton != null) _settingsButton.onClick.AddListener(OpenSettings);
        _escape.performed += QueueEscape;
        _escape.Enable();
        if (_ready)
        {
            BindServices();
            BindSettings();
        }
    }

    private void Start()
    {
        _ready = ValidateConfiguration();
        if (_ready)
        {
            BindServices();
            BindSettings();
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (_pauseButton != null) _pauseButton.onClick.RemoveListener(Pause);
        if (_resumeButton != null) _resumeButton.onClick.RemoveListener(Resume);
        if (_restartButton != null) _restartButton.onClick.RemoveListener(Restart);
        if (_mainMenuButton != null) _mainMenuButton.onClick.RemoveListener(MainMenu);
        if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OpenSettings);
        if (_audioSettings != null) _audioSettings.OnCloseRequested -= CloseSettings;
        _escape.performed -= QueueEscape;
        _escape.Disable();
        _escapePending = false;
        EndPause(restoreTime: !SceneFlow.IsLoading && IsPlayable);
        UnbindServices();
    }

    private void OnDestroy() => _escape?.Dispose();

    public void Pause()
    {
        if (!_ready || !isActiveAndEnabled || IsPaused || SceneFlow.IsLoading || !IsPlayable) return;
        if (!_phases.GameplayPhaseAllowsInput || Time.timeScale <= 0f
            || float.IsNaN(Time.timeScale) || float.IsInfinity(Time.timeScale)) return;

        _previousTimeScale = Time.timeScale;
        IsPaused = true;
        // Acquire first: closing another modal must never open an input gap.
        _phases.InputGate.Acquire(this);
        Time.timeScale = 0f;
        if (_bag != null) _bag.Close();
        if (_itemUse != null) _itemUse.Cancel();
        if (_treeMenu != null) _treeMenu.Close();
        _focusNavigator.SetDefaultTarget(_resumeButton, clearCurrentSelection: true);
        Refresh();
    }

    public void Resume()
    {
        if (!IsPaused || SceneFlow.IsLoading) return;
        EndPause(restoreTime: IsPlayable);
    }

    private void EndPause(bool restoreTime)
    {
        if (!IsPaused) return;
        _settingsOpen = false;
        IsPaused = false;
        // Rewards and results retain their own pause. SceneFlow owns the loading clock.
        if (restoreTime) Time.timeScale = _previousTimeScale;
        if (_phases != null) _phases.InputGate.Release(this, Time.frameCount);
        _focusNavigator.SetDefaultTarget(null, clearCurrentSelection: true);
        Refresh();
    }

    public void OpenSettings()
    {
        if (!IsPaused || _settingsOpen || SceneFlow.IsLoading) return;
        _settingsOpen = true;
        Refresh();
        _focusNavigator.SetDefaultTarget(_audioSettings.FirstControl, clearCurrentSelection: true);
    }

    public void CloseSettings()
    {
        if (!_settingsOpen || SceneFlow.IsLoading) return;
        _settingsOpen = false;
        _focusNavigator.SetDefaultTarget(_settingsButton, clearCurrentSelection: true);
        Refresh();
    }

    private void BindSettings()
    {
        _audioSettings.OnCloseRequested -= CloseSettings;
        _audioSettings.OnCloseRequested += CloseSettings;
    }

    private void QueueEscape(InputAction.CallbackContext context) => _escapePending = true;

    private void LateUpdate()
    {
        if (!_escapePending) return;
        _escapePending = false;
        if (SceneFlow.IsLoading) return;
        if (_settingsOpen) CloseSettings();
        else if (IsPaused) Resume();
        // Resolve after cancellation callbacks: releasing bag/target ownership blocks this frame.
        else if (_phases != null && _phases.GameplayInputAllowed) Pause();
    }

    private void Restart()
    {
        if (IsPaused && !SceneFlow.IsLoading) SceneFlow.RestartCurrent();
    }

    private void MainMenu()
    {
        if (IsPaused && !SceneFlow.IsLoading) SceneFlow.LoadMainMenu();
    }

    private void HandleReward(int night) => EndPause(restoreTime: false);
    private void HandleBeforeReward() => EndPause(restoreTime: IsPlayable);
    private void HandleFinished() => EndPause(restoreTime: false);
    private void HandleResult(RunResult result)
    {
        if (result != RunResult.Playing) EndPause(restoreTime: false);
        Refresh();
    }
    private void HandlePhase(int number) => Refresh();
    private void HandleLoading() => Refresh();

    private void BindServices()
    {
        UnbindServices();
        _phases = PhaseManager.Instance;
        _results = FindAnyObjectByType<WinLoseChecker>();
        _bag = FindAnyObjectByType<BagHUD>();
        _itemUse = FindAnyObjectByType<ItemUseController>();
        _treeMenu = FindAnyObjectByType<TreeElementMenu>();
        if (_phases == null || _results == null)
            Debug.LogError("PauseMenu requires a PhaseManager and WinLoseChecker in this level.", this);
        if (_phases != null)
        {
            _phases.OnDayStart += HandlePhase;
            _phases.OnNightStart += HandlePhase;
            _phases.OnRewardStart += HandleReward;
            _phases.OnBeforeRewardStart += HandleBeforeReward;
            _phases.OnRunFinished += HandleFinished;
        }
        if (_results != null) _results.OnResult += HandleResult;
        SceneFlow.OnLoadingChanged += HandleLoading;
        Refresh();
    }

    private void UnbindServices()
    {
        if (_phases != null)
        {
            _phases.OnDayStart -= HandlePhase;
            _phases.OnNightStart -= HandlePhase;
            _phases.OnRewardStart -= HandleReward;
            _phases.OnBeforeRewardStart -= HandleBeforeReward;
            _phases.OnRunFinished -= HandleFinished;
        }
        if (_results != null) _results.OnResult -= HandleResult;
        SceneFlow.OnLoadingChanged -= HandleLoading;
    }

    private void Refresh()
    {
        if (_pauseButton != null) _pauseButton.interactable = _ready && isActiveAndEnabled
            && !IsPaused && !SceneFlow.IsLoading && IsPlayable;
        // Never toggle arbitrary scene objects from an invalid prefab assignment.
        if (!_ready) return;
        if (HasSeparateOverlay()) _overlay.SetActive(IsPaused);
        if (_pausePanel != null && _pausePanel != gameObject) _pausePanel.SetActive(IsPaused && !_settingsOpen);
        if (_settingsPanel != null && _settingsPanel != gameObject) _settingsPanel.SetActive(IsPaused && _settingsOpen);
        bool canUse = IsPaused && !SceneFlow.IsLoading;
        if (_resumeButton != null) _resumeButton.interactable = canUse;
        if (_restartButton != null) _restartButton.interactable = canUse;
        if (_mainMenuButton != null) _mainMenuButton.interactable = canUse;
        if (_settingsButton != null) _settingsButton.interactable = canUse;
        if (_audioSettings != null) _audioSettings.SetInteractable(canUse && _settingsOpen);
    }

    private bool HasSeparateOverlay() => _overlay != null && _overlay != gameObject
        && _overlay.transform.IsChildOf(transform);

    private bool ValidateConfiguration()
    {
        if (!HasSeparateOverlay() || _pausePanel == null || _pausePanel == _overlay
            || !_pausePanel.transform.IsChildOf(_overlay.transform))
        {
            Debug.LogError("PauseMenu: assign a child overlay and a pause panel inside it.", this);
            return false;
        }
        if (_pauseButton != null && (!_pauseButton.transform.IsChildOf(transform)
            || _pauseButton.transform.IsChildOf(_overlay.transform)))
        {
            Debug.LogError("PauseMenu: if assigned, the optional pause button must be on this prefab outside the overlay.", this);
            return false;
        }
        Button[] buttons = { _resumeButton, _restartButton, _settingsButton, _mainMenuButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || !buttons[i].transform.IsChildOf(_pausePanel.transform))
            {
                Debug.LogError("PauseMenu: assign all four buttons inside the pause panel.", this);
                return false;
            }
            for (int j = 0; j < i; j++)
            {
                if (buttons[i] != buttons[j]) continue;
                Debug.LogError("PauseMenu: each menu action needs a distinct button.", this);
                return false;
            }
        }
        if (_settingsPanel == null || _settingsPanel == _overlay || _settingsPanel == _pausePanel
            || !_settingsPanel.transform.IsChildOf(_overlay.transform)
            || _settingsPanel.transform.IsChildOf(_pausePanel.transform)
            || _pausePanel.transform.IsChildOf(_settingsPanel.transform))
        {
            Debug.LogError("PauseMenu: assign a separate settings panel inside the overlay, beside the pause panel.", this);
            return false;
        }
        _audioSettings = AudioSettingsPanel.Resolve(_settingsPanel, _musicSlider, _sfxSlider, _settingsBackButton);
        if (_audioSettings == null)
        {
            Debug.LogError("PauseMenu: add AudioSettingsPanel to the settings panel and assign its controls.", this);
            return false;
        }
        if (!_audioSettings.TryValidate(out string error))
        {
            Debug.LogError("PauseMenu: " + error, this);
            return false;
        }
        return true;
    }
}
