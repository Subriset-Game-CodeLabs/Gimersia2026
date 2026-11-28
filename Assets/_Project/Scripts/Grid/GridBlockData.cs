using System;
using UnityEngine;

public class GridBlockData
{
    public const int TotalDirtyStages = 3;

    public string Id { get; }
    public GridState State { get; private set; }
    public float CleanProgress { get; private set; }
    public int RemainingDirtyStages { get; private set; }

    public event Action<GridBlockData> OnStateChanged;
    public event Action<GridBlockData> OnCleanProgressChanged;

    public GridBlockData(GridBlockDefinition definition)
    {
        Id = definition.Id;
        State = definition.StartState;
        CleanProgress = State == GridState.Clean ? 1f : 0f;
        RemainingDirtyStages = State == GridState.Clean ? 0 : TotalDirtyStages;
    }

    public void SetCleanProgress(float normalizedProgress)
    {
        if (State == GridState.Clean) return;

        float clamped = Mathf.Clamp01(normalizedProgress);
        int newRemainingStages = CalculateRemainingStages(clamped);

        if (Mathf.Approximately(CleanProgress, clamped) && RemainingDirtyStages == newRemainingStages)
            return;

        CleanProgress = clamped;
        RemainingDirtyStages = newRemainingStages;
        OnCleanProgressChanged?.Invoke(this);
    }

    public void SetState(GridState newState)
    {
        if (State == newState) return;
        State = newState;

        if (newState == GridState.Clean)
        {
            CleanProgress = 1f;
            RemainingDirtyStages = 0;
        }
        else
        {
            CleanProgress = 0f;
            RemainingDirtyStages = TotalDirtyStages;
        }

        OnCleanProgressChanged?.Invoke(this);
        OnStateChanged?.Invoke(this);
    }

    private static int CalculateRemainingStages(float normalizedProgress)
    {
        if (normalizedProgress >= 1f) return 0;
        if (normalizedProgress >= 2f / 3f) return 1;
        if (normalizedProgress >= 1f / 3f) return 2;
        return 3;
    }
}