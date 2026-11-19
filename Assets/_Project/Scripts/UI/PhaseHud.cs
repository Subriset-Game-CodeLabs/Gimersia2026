using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhaseHud : MonoBehaviour
{
    [SerializeField] private TMP_Text _phaseText;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private Button _startNightButton;

    private PhaseManager _phases;
    // Last whole second shown, so the timer string is rebuilt once per second, not every frame.
    private int _shownSeconds = -1;

    private void Start()
    {
        // PhaseManager sets Instance in Awake, so it exists by Start.
        _phases = PhaseManager.Instance;
        _phases.OnDayStart += HandleDayStart;
        _phases.OnNightStart += HandleNightStart;
        _phases.OnRunFinished += HandleRunFinished;
        _startNightButton.onClick.AddListener(_phases.StartNightEarly);
    }

    private void OnDestroy()
    {
        if (_phases == null) return;
        _phases.OnDayStart -= HandleDayStart;
        _phases.OnNightStart -= HandleNightStart;
        _phases.OnRunFinished -= HandleRunFinished;
        _startNightButton.onClick.RemoveListener(_phases.StartNightEarly);
    }

    private void Update()
    {
        // Only the countdown needs a per-frame read. Everything else is event-driven.
        if (_phases.CurrentPhase != PhaseType.Day) return;

        int seconds = Mathf.CeilToInt(_phases.TimeRemaining);
        if (seconds == _shownSeconds) return;

        _shownSeconds = seconds;
        _timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private void HandleDayStart(int day)
    {
        _phaseText.text = $"Day {day} / {_phases.TotalDays}";
        _startNightButton.interactable = true;
        _shownSeconds = -1; // force a refresh on the next Update
    }

    private void HandleNightStart(int night)
    {
        _phaseText.text = $"Night {night} / {_phases.TotalDays}";
        _timerText.text = "Defeat all enemies";
        // The button is only valid during the day.
        _startNightButton.interactable = false;
        _shownSeconds = -1;
    }

    private void HandleRunFinished()
    {
        _phaseText.text = "Run finished";
        _timerText.text = "";
        _startNightButton.interactable = false;
    }
}
