using System;
using UnityEngine;

[Serializable]
public class EnemySpawnGroup
{
    [SerializeField] private EnemyDefinition _enemyDefinition;
    [SerializeField] private EnemyController _enemyPrefab;
    [SerializeField] private string _spawnPointId;
    // TODO(balance): initial count and timing are authoring placeholders, not wave balance.
    [SerializeField, Min(1)] private int _count = 1;
    [SerializeField, Min(0.01f)] private float _spawnInterval = 0.5f;
    [SerializeField, Min(0f)] private float _initialDelay;

    public EnemyDefinition EnemyDefinition => _enemyDefinition;
    // Optional override; the level spawner's shared prefab is used when unassigned.
    public EnemyController EnemyPrefab => _enemyPrefab;
    public string SpawnPointId => _spawnPointId;
    public int Count => _count;
    public float SpawnInterval => _spawnInterval;
    public float InitialDelay => _initialDelay;
}
