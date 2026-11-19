using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Win/lose pop-up board with Restart and Main Menu. Hidden until the run ends.
public class ResultHud : MonoBehaviour
{
    // Optional: found in the scene when left empty, so the prefab drops into any level.
    [SerializeField] private WinLoseChecker _checker;
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private TMP_Text _subtitle;

    [Header("Placeholder icon (swap for the Win/Lose illustrations)")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _iconLabel;
    [SerializeField] private Color _winColor = new Color(0.38f, 0.70f, 0.36f);
    [SerializeField] private Color _loseColor = new Color(0.72f, 0.32f, 0.28f);

    [Header("Buttons")]
    [SerializeField] private Button _restartButton;
    // TODO: becomes "Back to Level Selection" once that screen exists.
    [SerializeField] private Button _mainMenuButton;

    private void Awake()
    {
        _panel.SetActive(false);

        if (_checker == null) _checker = FindAnyObjectByType<WinLoseChecker>();
        if (_checker == null)
        {
            Debug.LogError("ResultHud found no WinLoseChecker in the scene.", this);
            return;
        }
        _checker.OnResult += HandleResult;

        if (_restartButton != null) _restartButton.onClick.AddListener(SceneFlow.RestartCurrent);
        if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(SceneFlow.LoadMainMenu);
    }

    private void OnDestroy()
    {
        if (_checker != null) _checker.OnResult -= HandleResult;
        if (_restartButton != null) _restartButton.onClick.RemoveListener(SceneFlow.RestartCurrent);
        if (_mainMenuButton != null) _mainMenuButton.onClick.RemoveListener(SceneFlow.LoadMainMenu);
    }

    private void HandleResult(RunResult result)
    {
        bool won = result == RunResult.Won;
        _label.text = won ? "YOU WIN" : "YOU LOSE";

        if (_subtitle != null) _subtitle.text = BuildSubtitle(won);
        if (_icon != null) _icon.color = won ? _winColor : _loseColor;
        if (_iconLabel != null) _iconLabel.text = won ? "WIN" : "LOSE";

        _panel.SetActive(true);

        // A gamepad player needs something selected; the game is paused, UI still runs.
        if (_restartButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_restartButton.gameObject);
    }

    private static string BuildSubtitle(bool won)
    {
        PhaseManager phases = PhaseManager.Instance;
        if (phases == null)
            return won ? "The forest survived!" : "All trees have fallen.";

        return won
            ? $"The forest survived all {phases.TotalDays} nights!"
            : $"All trees have fallen on day {phases.CurrentDay}.";
    }
}
