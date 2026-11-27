using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class NightRewardController : MonoBehaviour
{
    [SerializeField] private BuffDefinition[] _pool;
    [SerializeField] private BuffSelectionHUD _hud;
    private readonly StageBuffState _state = new StageBuffState();
    private readonly List<BuffDefinition> _choices = new List<BuffDefinition>(3);
    private PhaseManager _phases;
    private bool _started;
    private bool _selecting;

    public StageBuffState State => _state;
    public IReadOnlyList<BuffDefinition> Choices => _choices.AsReadOnly();
    public bool IsSelecting => _selecting;
    public event Action OnChoicesChanged;

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
            if (!TryDraw(_pool, out BuffDefinition[] draw, out string error)) return Error(error);
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
        _state.Add(choice.Type, choice.Amount);
        _selecting = false; // Close the transaction before any listener can click again.
        _choices.Clear();
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

    // Draw by type first, then definition: multiple variants cannot produce duplicate types.
    public static bool TryDraw(IReadOnlyList<BuffDefinition> pool, out BuffDefinition[] choices, out string error)
    {
        choices = null;
        error = null;
        var groups = new Dictionary<BuffType, List<BuffDefinition>>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (pool == null || pool.Count < 3) { error = "Assign at least three valid buff definitions."; return false; }
        for (int i = 0; i < pool.Count; i++)
        {
            BuffDefinition definition = pool[i];
            if (definition == null) { error = $"Pool entry {i} is missing; assign or remove it."; return false; }
            if (!definition.TryValidate(out error)) { error = $"Pool entry {i} ({definition.name}): {error}"; return false; }
            if (!ids.Add(definition.Id)) { error = $"Duplicate buff ID '{definition.Id}'; use unique IDs."; return false; }
            if (!groups.TryGetValue(definition.Type, out List<BuffDefinition> group))
            {
                group = new List<BuffDefinition>();
                groups.Add(definition.Type, group);
            }
            group.Add(definition);
        }
        if (groups.Count < 3) { error = "Pool needs at least three distinct buff types (damage, attack speed, growth speed)."; return false; }
        var types = new List<BuffType>(groups.Keys);
        choices = new BuffDefinition[3];
        for (int i = 0; i < choices.Length; i++)
        {
            int selected = UnityEngine.Random.Range(i, types.Count);
            BuffType temp = types[i]; types[i] = types[selected]; types[selected] = temp;
            List<BuffDefinition> group = groups[types[i]];
            choices[i] = group[UnityEngine.Random.Range(0, group.Count)];
        }
        return true;
    }
}
