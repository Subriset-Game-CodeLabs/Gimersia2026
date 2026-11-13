using System;
using UnityEngine;

[Serializable]
public class GridBlockDefinition
{
    [SerializeField] private string _id;
    [SerializeField] private GridState _startState = GridState.Corrupted;

    // The prefab holds the block's final shape, position, and tree slots.
    [SerializeField] private BlockView _viewPrefab;

    public string Id => _id;
    public GridState StartState => _startState;
    public BlockView ViewPrefab => _viewPrefab;
}