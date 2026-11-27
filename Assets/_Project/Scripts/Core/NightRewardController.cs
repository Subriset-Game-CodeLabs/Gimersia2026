using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class NightRewardController : MonoBehaviour
{
    [SerializeField] private BuffDefinition[] _pool;
    [SerializeField] private BuffSelectionHUD _hud;
    [SerializeField] private BuffCapSettings _statCaps;
    private readonly StageBuffState _state = new StageBuffState();
    private readonly List<BuffDefinition> _choices = new List<BuffDefinition>(3);
    private PhaseManager _phases;
    private bool _started;
    private bool _selecting;

    public StageBuffState State => _state;
    public IReadOnlyList<BuffDefinition> Choices => _choices.AsReadOnly();
    public bool IsSelecting => _selecting;
    public event Action OnChoicesChanged;
    public event Action<BuffDefinition> OnCardChosen;

    private void Start()
    {
        _started = true;
        Subscribe();
    }

    private void OnEnable() { if (_started) Subscribe(); }
    private void Subscribe()
    {
        _phases = PhaseManager.Instance;
        if (_phases == null || _phases.Rewards != this)
        {
            Debug.LogError("NightRewardController must be assigned to the scene PhaseManager's Rewards field.", this);
            return;
        }
        _phases.OnRunFinished += Cancel;
        if (_phases.CurrentPhase == PhaseType.RewardSelection) BeginSelection();
        else if (_phases.CurrentPhase == PhaseType.Finished) Cancel();
    }

    private void OnDisable()
    {
        if (_phases != null) _phases.OnRunFinished -= Cancel;
        // Preserve choices and the phase-owned pause; re-enable resumes this transaction.
        if (_selecting) Debug.LogError("Reward controller disabled during selection. Re-enable it to continue; reward is not skipped.", this);
    }

    public bool BeginSelection()
    {
        if (!isActiveAndEnabled) return Error("Enable NightRewardController before completing a night.");
        if (_phases == null) _phases = PhaseManager.Instance;
        if (_phases == null || _phases.Rewards != this || _phases.CurrentPhase != PhaseType.RewardSelection) return false;
        if (_hud == null || !_hud.IsConfiguredFor(this)) return Error("Assign an enabled BuffSelectionHUD with three configured distinct cards and an active host outside its panel.");
        if (!_selecting)
        {
            if (!TryDraw(_pool, _state, _statCaps, out BuffDefinition[] draw, out string error)) return Error(error);
            if (draw.Length == 0) return _phases.CompleteReward(this);
            _choices.Clear();
            _choices.AddRange(draw);
            _selecting = true;
        }
        OnChoicesChanged?.Invoke();
        return true;
    }

    public bool TryChoose(int index)
    {
        if (!isActiveAndEnabled || !_selecting || index < 0 || index >= _choices.Count
            || _phases == null || !_phases.CanCompleteReward(this)) return false;
        BuffDefinition choice = _choices[index];
        if (choice == null || !choice.TryValidate(out _)) return Error("An offered buff became invalid. Restore its definition before selecting.");
        if (!_state.TryApply(choice, _statCaps)) return Error("This card no longer has an applicable effect.");
        _selecting = false; // Close the transaction before any listener can click again.
        _choices.Clear();
        OnCardChosen?.Invoke(choice);
        OnChoicesChanged?.Invoke();
        return _phases.CompleteReward(this);
    }

    private void Cancel()
    {
        _selecting = false;
        _choices.Clear();
        OnChoicesChanged?.Invoke();
    }

    private bool Error(string message)
    {
        Debug.LogError("Night reward: " + message + " Selection stays blocked; fix configuration, then re-enable the controller.", this);
        return false;
    }

    public static bool TryDraw(IReadOnlyList<BuffDefinition> pool, out BuffDefinition[] choices, out string error)
        => TryDraw(pool, null, null, out choices, out error);

    private static bool TryDraw(IReadOnlyList<BuffDefinition> pool, StageBuffState state, BuffCapSettings caps, out BuffDefinition[] choices, out string error)
    {
        choices = null;
        error = null;
        var eligible = new List<BuffDefinition>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (pool == null || pool.Count < 3) { error = "Assign at least three valid buff definitions."; return false; }
        for (int i = 0; i < pool.Count; i++)
        {
            BuffDefinition definition = pool[i];
            if (definition == null) { error = $"Pool entry {i} is missing; assign or remove it."; return false; }
            if (!definition.TryValidate(out error)) { error = $"Pool entry {i} ({definition.name}): {error}"; return false; }
            if (!ids.Add(definition.Id)) { error = $"Duplicate buff ID '{definition.Id}'; use unique IDs."; return false; }
            if (state == null || state.CanApply(definition, caps)) eligible.Add(definition);
        }
        choices = new BuffDefinition[Math.Min(3, eligible.Count)];
        for (int i = 0; i < choices.Length; i++)
        {
            int selected = UnityEngine.Random.Range(i, eligible.Count);
            choices[i] = eligible[selected];
            eligible[selected] = eligible[i];
        }
        return true;
    }
}
