using System;
using UnityEngine;

public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance { get; private set; }

    [SerializeField] private PhaseSettings _settings;
    [SerializeField] private NightRewardController _rewards;
    [SerializeField] private WinLoseChecker _results;
    public NightRewardController Rewards => _rewards;
    public StageBuffState Buffs => _rewards != null ? _rewards.State : null;
    public event Action<int> OnRewardStart;
    // Release manual pause before the reward transaction snapshots the gameplay clock.
    public event Action OnBeforeRewardStart;
    private int _inputBlockedFrame = -1;
    private float _preRewardTimeScale = 1f;
    private bool _rewardPaused;
    public GameplayInputGate InputGate { get; } = new GameplayInputGate();
    public bool GameplayInputAllowed => GameplayPhaseAllowsInput && !InputGate.IsBlocked(Time.frameCount);
    // Item UI checks the phase without rejecting the input lock it owns itself.
    public bool GameplayPhaseAllowsInput => (CurrentPhase == PhaseType.Day || CurrentPhase == PhaseType.Night)
        && Time.frameCount > _inputBlockedFrame && (_results == null || _results.Result == RunResult.Playing);

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
    public bool CanStartNightEarly => CurrentPhase == PhaseType.Day && GameplayInputAllowed;

    private bool _started;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (_results != null) _results.OnResult -= HandleResult;
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (_results == null) _results = FindAnyObjectByType<WinLoseChecker>();
        if (_results != null) _results.OnResult += HandleResult;
        else Debug.LogError("PhaseManager requires a WinLoseChecker for reward/result priority.", this);
        if (_rewards == null) Debug.LogError("Assign NightRewardController to PhaseManager.Rewards; non-final nights will block until configured.", this);
    }

    private void HandleResult(RunResult result)
    {
        if (result != RunResult.Playing) Finish();
    }

    private void Update()
    {
        // Begin on the first Update, not in Start, so every subscriber has
        // already subscribed in its own Start and sees day 1.
        if (!_started)
        {
            _started = true;
            if (_results != null && _results.Result != RunResult.Playing) return;
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
        if (_results == null || _results.Result != RunResult.Playing) return;
        // An empty forest must lose, even if wave completion arrives before the tree-death listener.
        if (TreeRegistry.Instance != null && TreeRegistry.Instance.AliveCount == 0)
        {
            Finish();
            return;
        }
        AdvancePhase();
    }

    public bool CanCompleteReward(NightRewardController owner) => owner != null && owner == _rewards
        && CurrentPhase == PhaseType.RewardSelection && _results != null && _results.Result == RunResult.Playing;

    public bool CompleteReward(NightRewardController owner)
    {
        if (!CanCompleteReward(owner)) return false;
        _inputBlockedFrame = Time.frameCount; // Do not let the selecting click reach the world this frame.
        if (_rewardPaused) Time.timeScale = _preRewardTimeScale;
        _rewardPaused = false;
        BeginDay(CurrentDay + 1);
        return true;
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
            OnBeforeRewardStart?.Invoke();
            if (CurrentPhase != PhaseType.Night || _results == null || _results.Result != RunResult.Playing) return;
            CurrentPhase = PhaseType.RewardSelection;
            TimeRemaining = PhaseDuration = 0f;
            _inputBlockedFrame = Time.frameCount;
            _preRewardTimeScale = Time.timeScale;
            _rewardPaused = true;
            Time.timeScale = 0f;

            AudioManager.Instance.PlayNightCleared();
            AudioManager.Instance.PlayDayMusic();

            OnRewardStart?.Invoke(CurrentDay);
            if (CurrentPhase != PhaseType.RewardSelection) return;
            if (_rewards != null) _rewards.BeginSelection();
            else Debug.LogError("Night cleared but Rewards is missing. Assign NightRewardController; no reward is skipped.", this);
        }
    }

    private void BeginDay(int day)
    {
        CurrentDay = day;
        CurrentPhase = PhaseType.Day;
        PhaseDuration = _settings.DayDuration;
        TimeRemaining = PhaseDuration;

        AudioManager.Instance.PlayDayMusic();
        AudioManager.Instance.PlayPhaseDayStart();

        OnDayStart?.Invoke(day);
    }

    private void BeginNight()
    {
        // Day one may start empty, but night requires a grown survivor.
        // Resolve before publishing night so no listener can start a wave after defeat.
        if (TreeRegistry.Instance != null && TreeRegistry.Instance.AliveCount == 0)
        {
            Finish();
            return;
        }

        CurrentPhase = PhaseType.Night;
        PhaseDuration = 0f;
        TimeRemaining = 0f;

        AudioManager.Instance.PlayNightMusic();
        AudioManager.Instance.PlayPhaseNightStart();

        OnNightStart?.Invoke(CurrentDay);
    }

    private void Finish()
    {
        if (_started && CurrentPhase == PhaseType.Finished) return;
        _started = true;
        CurrentPhase = PhaseType.Finished;
        _rewardPaused = false; // Result owns the pause; cancellation must never restore time scale.
        TimeRemaining = 0f;
        OnRunFinished?.Invoke();
    }
}