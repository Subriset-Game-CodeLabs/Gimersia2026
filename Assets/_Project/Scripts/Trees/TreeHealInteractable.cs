using UnityEngine;

[RequireComponent(typeof(TreeNode))]
public class TreeHealInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private TeamworkSettings _teamworkSettings;
    [SerializeField] private float _hpPerSecond = 10f;
    [SerializeField] private float _nightSpeedMultiplier = 0.5f;
    [SerializeField] private bool _logHealing = true;

    private TreeNode _node;
    private WorkerSet _workers;
    private bool _loggingSession;
    private float _lastLogTime;
    private float _healedSinceLog;
    private PhaseType _loggedPhase;

    public Vector3 InteractionPoint => _node.InteractionPoint;
    public bool CanInteract =>
        _node != null && _node.Data != null && _node.IsTargetable &&
        _node.Data.NeedsHealing && _workers != null;

    private void Awake()
    {
        _node = GetComponent<TreeNode>();
        if (_teamworkSettings == null)
        {
            Debug.LogError("TreeHealInteractable needs TeamworkSettings assigned.", this);
            return;
        }
        _workers = new WorkerSet(_teamworkSettings);
    }

    public void AddWorker(SpiritController spirit)
    {
        if (_workers == null) return;
        _workers.Add(spirit);
        if (!_logHealing || _loggingSession || _workers.Count == 0) return;

        _loggingSession = true;
        _lastLogTime = Time.time;
        _healedSinceLog = 0f;
        _loggedPhase = GetPhase();
        Debug.Log($"[Heal] {name}: started in {_loggedPhase}, " +
                  $"HP {_node.Data.Hp:0.0}/{_node.Data.MaxHp:0.0}", this);
    }

    public void RemoveWorker(SpiritController spirit)
    {
        if (_workers == null) return;
        _workers.Remove(spirit);
        if (_workers.Count != 0 || !_loggingSession) return;
        LogProgress("stopped");
        _loggingSession = false;
    }

    public void Interact(SpiritController spirit, float deltaTime)
    {
        if (!CanInteract) return;

        PhaseType phase = GetPhase();
        if (_loggingSession && phase != _loggedPhase)
        {
            LogProgress("phase changed");
            _loggedPhase = phase;
        }
        float phaseMultiplier = phase == PhaseType.Night
            ? _nightSpeedMultiplier
            : 1f;

        float work = _workers.GetShare(deltaTime) * spirit.WorkSpeedMultiplier;
        float previousHp = _node.Data.Hp;
        _node.Data.Heal(work * Mathf.Max(0f, _hpPerSecond) *
                        Mathf.Max(0f, phaseMultiplier));

        AudioManager.Instance.PlayTreeHealTick(InteractionPoint);

        if (!_loggingSession) return;

        _healedSinceLog += _node.Data.Hp - previousHp;
        if (!CanInteract)
        {
            LogProgress("fully healed");
            _loggingSession = false;
        }
        else if (Time.time - _lastLogTime >= 1f)
        {
            LogProgress("progress");
        }
    }

    private PhaseType GetPhase()
    {
        PhaseManager phases = PhaseManager.Instance;
        return phases != null ? phases.CurrentPhase : PhaseType.Day;
    }

    private void LogProgress(string reason)
    {
        if (!_logHealing || !_loggingSession) return;

        float seconds = Time.time - _lastLogTime;
        float rate = seconds > 0f ? _healedSinceLog / seconds : 0f;
        Debug.Log($"[Heal] {name}: {reason}, phase={_loggedPhase}, " +
                  $"spirits={_workers.Count}, " +
                  $"HP={_node.Data.Hp:0.0}/{_node.Data.MaxHp:0.0}, " +
                  $"+{_healedSinceLog:0.0} in {seconds:0.0}s " +
                  $"({rate:0.0} HP/s)", this);
        _healedSinceLog = 0f;
        _lastLogTime = Time.time;
    }

    private void OnDisable()
    {
        if (!_loggingSession) return;
        LogProgress("disabled");
        _loggingSession = false;
    }
}
