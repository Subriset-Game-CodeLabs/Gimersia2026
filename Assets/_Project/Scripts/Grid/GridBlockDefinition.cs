using System;
using UnityEngine;

[Serializable]
public class GridBlockDefinition
{
    [SerializeField] private string _id;
    [SerializeField] private Vector2Int _cell;
    [SerializeField] private GridState _startState = GridState.Corrupted;
    [SerializeField] private Vector2[] _treeSlotOffsets;

    public string Id => _id;
    public Vector2Int Cell => _cell;
    public GridState StartState => _startState;
    public Vector2[] TreeSlotOffsets => _treeSlotOffsets;

    public GridBlockDefinition(string id, Vector2Int cell, Vector2[] treeSlotOffsets)
    {
        _id = id;
        _cell = cell;
        _treeSlotOffsets = treeSlotOffsets;
    }
}