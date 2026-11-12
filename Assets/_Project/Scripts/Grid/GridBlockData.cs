using System;
using UnityEngine;

public class GridBlockData
{
    public string Id { get; }
    public Vector2Int Cell { get; }
    public Vector2[] TreeSlotOffsets { get; }
    public GridState State { get; private set; }

    public event Action<GridBlockData> OnStateChanged;

    public GridBlockData(GridBlockDefinition definition)
    {
        Id = definition.Id;
        Cell = definition.Cell;
        TreeSlotOffsets = definition.TreeSlotOffsets;
        State = definition.StartState;
    }

    public void SetState(GridState newState)
    {
        if (State == newState) return;
        State = newState;
        OnStateChanged?.Invoke(this);
    }
}