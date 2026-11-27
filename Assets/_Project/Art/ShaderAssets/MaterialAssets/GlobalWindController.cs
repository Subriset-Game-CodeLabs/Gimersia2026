using UnityEngine;

public class GlobalWindController : MonoBehaviour
{
    private static readonly int GlobalWindStopId = Shader.PropertyToID("_GlobalWindStop");

    [Header("Transisi Angin Siang & Malam")]
    [Tooltip("Durasi angin mereda hingga berhenti total saat masuk malam (detik).")]
    [SerializeField, Min(0f)] private float _windFadeDuration = 1.8f;

    [Tooltip("Jika dicentang, angin langsung berhenti seketika tanpa transisi pelan.")]
    [SerializeField] private bool _instantTransition = false;

    private PhaseManager _phases;
    private float _currentWindStop;
    private float _targetWindStop;

    private void OnEnable()
    {
        TryBindPhaseManager(instant: true);
    }

    private void Start()
    {
        TryBindPhaseManager(instant: true);
    }

    private void OnDisable()
    {
        UnbindPhaseManager();
        // Reset ke 0 agar preview angin di Scene View Editor tetap aktif saat keluar dari Play Mode
        Shader.SetGlobalFloat(GlobalWindStopId, 0f);
    }

    private void Update()
    {
        if (_phases == null)
            TryBindPhaseManager(instant: true);

        if (Mathf.Approximately(_currentWindStop, _targetWindStop))
            return;

        if (_instantTransition || _windFadeDuration <= 0.01f)
        {
            _currentWindStop = _targetWindStop;
        }
        else
        {
            float speed = 1f / _windFadeDuration;
            _currentWindStop = Mathf.MoveTowards(_currentWindStop, _targetWindStop, speed * Time.deltaTime);
        }

        Shader.SetGlobalFloat(GlobalWindStopId, _currentWindStop);
    }

    private void TryBindPhaseManager(bool instant)
    {
        PhaseManager current = PhaseManager.Instance;
        if (current == null) return;

        if (_phases != current)
        {
            UnbindPhaseManager();
            _phases = current;
            _phases.OnDayStart += HandleDayStart;
            _phases.OnNightStart += HandleNightStart;
        }

        bool isNight = _phases.CurrentPhase == PhaseType.Night;
        SetWindStopped(isNight, instant);
    }

    private void UnbindPhaseManager()
    {
        if (_phases == null) return;
        _phases.OnDayStart -= HandleDayStart;
        _phases.OnNightStart -= HandleNightStart;
        _phases = null;
    }

    private void HandleDayStart(int day)
    {
        SetWindStopped(stopped: false, instant: _instantTransition);
    }

    private void HandleNightStart(int night)
    {
        SetWindStopped(stopped: true, instant: _instantTransition);
    }

    public void SetWindStopped(bool stopped, bool instant = false)
    {
        _targetWindStop = stopped ? 1f : 0f;

        if (instant || _windFadeDuration <= 0.01f)
        {
            _currentWindStop = _targetWindStop;
            Shader.SetGlobalFloat(GlobalWindStopId, _currentWindStop);
        }
    }
}