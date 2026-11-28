using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhaseHud : MonoBehaviour
{
    [Header("Clock & Phase")]
    [SerializeField] private TMP_Text _phaseText;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private Image _timerPillImage;
    [SerializeField] private Sprite _dayTimerPillSprite;
    [SerializeField] private Sprite _nightTimerPillSprite;
    [SerializeField] private RectTransform _clockHand;
    [SerializeField] private Color _dayLabelColor = new Color(1f, 0.905f, 0.48f, 1f);
    [SerializeField] private Color _nightLabelColor = new Color(0.75f, 0.80f, 1f, 1f);

    [Header("Wave Counter")]
    [SerializeField] private TMP_Text _waveText;
    [SerializeField] private Image[] _waveFlags;
    [SerializeField] private Sprite _flagDoneSprite;
    [SerializeField] private Sprite _flagCurrentSprite;
    [SerializeField] private Sprite _flagUpcomingSprite;

    [Header("Start Night & Pause")]
    [SerializeField] private Button _startNightButton;
    [SerializeField] private CanvasGroup _startNightGroup;
    [SerializeField] private Button _pauseButton;

    // Continuous clockwise clock coordinates (Unity Z decreases clockwise):
    // +90 deg = Dawn (left horizon) -> 0 deg = Noon (top) -> -90 deg = Dusk (right horizon)
    // -> -180 deg = Midnight (bottom) -> -270 deg (= +90 deg) = Dawn again.
    private const float DawnAngle = 90f;
    private const float DuskAngle = -90f;
    private const float NextDawnAngle = -270f;
    private const float NightBaselineDuration = 75f;

    private PhaseManager _phases;
    private EnemySpawner _spawner;
    private PauseMenu _pauseMenu;
    private int _shownSeconds = -1;
    private float _nightElapsed;
    private float _currentHandAngle = DawnAngle;
    private bool _handInitialized;
    private Tween _startNightTween;
    private bool _startNightVisible = true;

    private void Start()
    {
        _phases = PhaseManager.Instance;
        _spawner = FindAnyObjectByType<EnemySpawner>();
        _pauseMenu = FindAnyObjectByType<PauseMenu>();

        if (_phases != null)
        {
            _phases.OnDayStart += HandleDayStart;
            _phases.OnNightStart += HandleNightStart;
            _phases.OnRewardStart += HandleRewardStart;
            _phases.OnRunFinished += HandleRunFinished;
        }

        if (_startNightButton != null)
            _startNightButton.onClick.AddListener(HandleStartNightClicked);

        if (_pauseButton != null)
            _pauseButton.onClick.AddListener(HandlePauseClicked);

        if (_phases != null && _phases.CurrentDay > 0)
        {
            if (_phases.CurrentPhase == PhaseType.Day) HandleDayStart(_phases.CurrentDay);
            else if (_phases.CurrentPhase == PhaseType.Night) HandleNightStart(_phases.CurrentDay);
            else if (_phases.CurrentPhase == PhaseType.RewardSelection) HandleRewardStart(_phases.CurrentDay);
            else HandleRunFinished();
        }
        else
        {
            RefreshWaveCounter(1);
            ApplyHandAngle(DawnAngle);
            _handInitialized = true;
        }
    }

    private void OnDestroy()
    {
        _startNightTween?.Kill();

        if (_phases != null)
        {
            _phases.OnDayStart -= HandleDayStart;
            _phases.OnNightStart -= HandleNightStart;
            _phases.OnRewardStart -= HandleRewardStart;
            _phases.OnRunFinished -= HandleRunFinished;
        }

        if (_startNightButton != null)
            _startNightButton.onClick.RemoveListener(HandleStartNightClicked);

        if (_pauseButton != null)
            _pauseButton.onClick.RemoveListener(HandlePauseClicked);
    }

    private void Update()
    {
        if (_phases == null) return;

        if (_pauseButton != null)
        {
            bool playable = (_phases.CurrentPhase == PhaseType.Day || _phases.CurrentPhase == PhaseType.Night)
                && (_pauseMenu == null || !_pauseMenu.IsPaused);
            if (_pauseButton.interactable != playable)
            {
                _pauseButton.interactable = playable;
                if (_pauseButton.TryGetComponent(out UIStateButton stateBtn)) stateBtn.Refresh();
            }
        }

        if (_phases.CurrentPhase == PhaseType.Day)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(_phases.TimeRemaining));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                if (_timerText != null)
                    _timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }

            if (_phases.PhaseDuration > 0.01f)
            {
                float progress = 1f - Mathf.Clamp01(_phases.TimeRemaining / _phases.PhaseDuration);
                float targetAngle = Mathf.Lerp(DawnAngle, DuskAngle, progress);
                AdvanceHandClockwiseToward(targetAngle, Time.deltaTime, 220f);
            }
        }
        else if (_phases.CurrentPhase == PhaseType.Night)
        {
            _nightElapsed += Time.deltaTime;
            int seconds = Mathf.FloorToInt(_nightElapsed);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                if (_timerText != null)
                    _timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }

            float nightProgress = ComputeNightProgress();
            float targetAngle = Mathf.Lerp(DuskAngle, NextDawnAngle + 6f, nightProgress);
            AdvanceHandClockwiseToward(targetAngle, Time.deltaTime, 120f);
        }
        else if (_phases.CurrentPhase == PhaseType.RewardSelection)
        {
            // Gently finish sweeping to the Dawn horizon while the player picks a blessing card.
            AdvanceHandClockwiseToward(NextDawnAngle, Time.unscaledDeltaTime, 160f);
        }
    }

    private float ComputeNightProgress()
    {
        // Always crawl forward continuously with time so the clock hand never feels frozen,
        // and accelerate smoothly with real wave clear progress as enemies are defeated.
        float timeProgress = 1f - Mathf.Exp(-_nightElapsed / NightBaselineDuration);
        float enemyProgress = 0f;

        if (_spawner == null) _spawner = FindAnyObjectByType<EnemySpawner>();
        if (_spawner != null && _spawner.TotalWaveEnemies > 0)
        {
            int remaining = Mathf.Max(0, _spawner.RemainingSpawns + _spawner.AliveCount);
            int defeated = Mathf.Clamp(_spawner.TotalWaveEnemies - remaining, 0, _spawner.TotalWaveEnemies);
            enemyProgress = (float)defeated / _spawner.TotalWaveEnemies;
        }

        return Mathf.Clamp(Mathf.Max(timeProgress * 0.78f, enemyProgress * 0.94f), 0f, 0.97f);
    }

    private void AdvanceHandClockwiseToward(float targetAngle, float dt, float maxDegreesPerSecond)
    {
        if (_clockHand == null) return;

        // Normalize target so it is always <= _currentHandAngle (strictly clockwise forward, never jumping backward).
        while (targetAngle > _currentHandAngle + 0.5f)
            targetAngle -= 360f;

        float delta = _currentHandAngle - targetAngle;
        if (delta <= 0.01f)
            return;

        // Fast glide if catching up across a phase transition (e.g. Start Night Early or Dawn transition),
        // silky smooth follow during steady ticking.
        float speed = delta > 20f ? Mathf.Max(maxDegreesPerSecond, delta * 3.5f) : maxDegreesPerSecond;
        _currentHandAngle = Mathf.MoveTowards(_currentHandAngle, targetAngle, speed * dt);

        // Keep angle numerically bounded without visual discontinuity.
        if (_currentHandAngle <= -270f && targetAngle <= -270f)
            _currentHandAngle += 360f;

        ApplyHandAngle(_currentHandAngle);
    }

    private void ApplyHandAngle(float angleZ)
    {
        _currentHandAngle = angleZ;
        if (_clockHand != null)
            _clockHand.localEulerAngles = new Vector3(0f, 0f, angleZ);
    }

    private void HandleStartNightClicked()
    {
        if (_phases == null || !_phases.CanStartNightEarly) return;
        AudioManager.Instance.PlayUiClick();
        _phases.StartNightEarly();
    }

    private void HandlePauseClicked()
    {
        if (_pauseMenu == null) _pauseMenu = FindAnyObjectByType<PauseMenu>();
        if (_pauseMenu == null) return;
        AudioManager.Instance.PlayUiClick();
        _pauseMenu.Pause();
    }

    private void HandleDayStart(int day)
    {
        if (_phaseText != null)
        {
            _phaseText.text = "DAY";
            _phaseText.color = _dayLabelColor;
        }

        if (_timerPillImage != null && _dayTimerPillSprite != null)
            _timerPillImage.sprite = _dayTimerPillSprite;

        _shownSeconds = -1;
        SetStartNightVisible(true);
        RefreshWaveCounter(day);

        if (!_handInitialized)
        {
            ApplyHandAngle(DawnAngle);
            _handInitialized = true;
        }
        // If already running (e.g. after Night -> Reward -> Day 2), keep _currentHandAngle as-is so
        // Update() smoothly sweeps clockwise across the Dawn horizon instead of teleporting!
    }

    private void HandleNightStart(int night)
    {
        if (_phaseText != null)
        {
            _phaseText.text = "NIGHT";
            _phaseText.color = _nightLabelColor;
        }

        if (_timerPillImage != null && _nightTimerPillSprite != null)
            _timerPillImage.sprite = _nightTimerPillSprite;

        _nightElapsed = 0f;
        _shownSeconds = -1;
        if (_timerText != null) _timerText.text = "00:00";

        SetStartNightVisible(false);
        RefreshWaveCounter(night);

        if (!_handInitialized)
        {
            ApplyHandAngle(DuskAngle);
            _handInitialized = true;
        }
    }

    private void HandleRewardStart(int night)
    {
        if (_phaseText != null)
        {
            _phaseText.text = "CLEAR";
            _phaseText.color = _dayLabelColor;
        }
        if (_timerText != null) _timerText.text = "--:--";
        SetStartNightVisible(false);
        RefreshWaveCounter(night);
    }

    private void HandleRunFinished()
    {
        if (_phaseText != null)
        {
            _phaseText.text = "END";
            _phaseText.color = _dayLabelColor;
        }
        if (_timerText != null) _timerText.text = "00:00";
        SetStartNightVisible(false);
    }

    private void RefreshWaveCounter(int currentWave)
    {
        int totalWaves = _phases != null ? Mathf.Max(1, _phases.TotalDays) : 7;
        int clamped = Mathf.Clamp(currentWave, 1, totalWaves);

        if (_waveText != null)
            _waveText.text = $"WAVE {clamped}/{totalWaves}";

        if (_waveFlags == null || _waveFlags.Length == 0) return;

        for (int i = 0; i < _waveFlags.Length; i++)
        {
            Image flag = _waveFlags[i];
            if (flag == null) continue;

            bool show = i < totalWaves;
            flag.gameObject.SetActive(show);
            if (!show) continue;

            if (i < clamped - 1)
            {
                if (_flagDoneSprite != null) flag.sprite = _flagDoneSprite;
            }
            else if (i == clamped - 1)
            {
                if (_flagCurrentSprite != null) flag.sprite = _flagCurrentSprite;
                flag.rectTransform.DOKill();
                flag.rectTransform.localScale = Vector3.one;
                flag.rectTransform.DOPunchScale(Vector3.one * 0.16f, 0.28f, 4, 0.5f)
                    .SetUpdate(true)
                    .SetLink(flag.gameObject);
            }
            else
            {
                if (_flagUpcomingSprite != null) flag.sprite = _flagUpcomingSprite;
            }
        }
    }

    private void SetStartNightVisible(bool visible)
    {
        if (_startNightButton == null) return;

        _startNightButton.interactable = visible;
        if (_startNightButton.TryGetComponent(out UIStateButton stateBtn))
            stateBtn.Refresh();

        if (_startNightGroup == null)
        {
            _startNightButton.gameObject.SetActive(visible);
            _startNightVisible = visible;
            return;
        }

        if (_startNightVisible == visible && _startNightButton.gameObject.activeSelf == visible)
            return;

        _startNightVisible = visible;
        _startNightTween?.Kill();

        if (visible)
        {
            _startNightButton.gameObject.SetActive(true);
            _startNightGroup.blocksRaycasts = true;
            _startNightGroup.interactable = true;
            _startNightGroup.alpha = 0f;
            _startNightButton.transform.localScale = Vector3.one * 0.92f;
            _startNightTween = DOTween.Sequence()
                .Join(_startNightGroup.DOFade(1f, 0.2f))
                .Join(_startNightButton.transform.DOScale(1f, 0.22f).SetEase(Ease.OutBack))
                .SetUpdate(true)
                .SetLink(_startNightButton.gameObject);
        }
        else
        {
            _startNightGroup.blocksRaycasts = false;
            _startNightGroup.interactable = false;
            _startNightTween = DOTween.Sequence()
                .Join(_startNightGroup.DOFade(0f, 0.18f))
                .Join(_startNightButton.transform.DOScale(0.92f, 0.18f).SetEase(Ease.InQuad))
                .SetUpdate(true)
                .SetLink(_startNightButton.gameObject)
                .OnComplete(() =>
                {
                    if (!_startNightVisible && _startNightButton != null)
                        _startNightButton.gameObject.SetActive(false);
                });
        }
    }
}
