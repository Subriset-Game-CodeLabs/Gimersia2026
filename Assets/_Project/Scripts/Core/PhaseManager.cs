

using System;
using UnityEngine;

public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance { get; private set; }

    [SerializeField] private PhaseSettings _settings;

    // The int is the day or night number (1 to TotalDays).
    public event Action<int> OnDayStart;
    public event Action<int> OnNightStart;
    
    // Raised once after the last night ends. Win/lose evaluation hooks in here later.
    public event Action OnRunFinished;

    public PhaseType CurrentPhase { get; private set; } = PhaseType.Finished;
    public int CurrentDay { get; private set; }
    public float TimeRemaining { get; private set; }
    public float PhaseDuration { get; private set; }
    public int TotalDays => _settings.TotalDays;
    public bool CanStartNightEarly => CurrentPhase == PhaseType.Day;

    private bool _started;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Begin on the first Update, not in Start, so every subscriber has
        // already subscribed in its own Start and sees day 1.
        if (!_started)
        {
            _started = true;
            BeginDay(1);
        }

        // Nights end when the spawner reports the last enemy defeated.
        if (CurrentPhase != PhaseType.Day) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f) AdvancePhase();
    }

    // Called by the UI button. Only valid during the day.
    public void StartNightEarly()
    {
        if (!CanStartNightEarly) return;
        BeginNight();
    }

    public void CompleteNight()
    {
        if (CurrentPhase != PhaseType.Night) return;
        AdvancePhase();
    }

    private void AdvancePhase()
    {
        if (CurrentPhase == PhaseType.Day)
        {
            BeginNight();
        }
        else if (CurrentDay >= _settings.TotalDays)
        {
            Finish();
        }
        else
        {
            BeginDay(CurrentDay + 1);
        }
    }

    private void BeginDay(int day)
    {
        CurrentDay = day;
        CurrentPhase = PhaseType.Day;
        PhaseDuration = _settings.DayDuration;
        TimeRemaining = PhaseDuration;
        OnDayStart?.Invoke(day);
    }

    private void BeginNight()
    {
        CurrentPhase = PhaseType.Night;
        PhaseDuration = 0f;
        TimeRemaining = 0f;
        OnNightStart?.Invoke(CurrentDay);
    }

    private void Finish()
    {
        CurrentPhase = PhaseType.Finished;
        TimeRemaining = 0f;
        OnRunFinished?.Invoke();
    }
}