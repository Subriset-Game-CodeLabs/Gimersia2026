using System;
using UnityEngine;

public class GridBlockData
{
    public string Id { get; }
    public GridState State { get; private set; }

    public event Action<GridBlockData> OnStateChanged;

    public GridBlockData(GridBlockDefinition definition)
    {
        Id = definition.Id;
        State = definition.StartState;
    }

    public void SetState(GridState newState)
    {
        if (State == newState) return;
        State = newState;
        OnStateChanged?.Invoke(this);
    }
}