using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemySpawnGroup
{
    [SerializeField] private EnemyDefinition _enemyDefinition;
    [SerializeField] private EnemyController _enemyPrefab;

    [Tooltip("Daftar ID Spawn Point untuk jenis musuh ini. Tiap musuh akan muncul secara acak di salah satu titik ini.")]
    [SerializeField] private List<string> _spawnPointIds = new List<string>();

    [Tooltip("Cadangan jika daftar Spawn Point Ids di atas dikosongkan.")]
    [SerializeField] private string _spawnPointId;

    [SerializeField, Min(1)] private int _count = 1;
    [SerializeField, Min(0.01f)] private float _spawnInterval = 0.5f;
    [SerializeField, Min(0f)] private float _initialDelay;

    public EnemyDefinition EnemyDefinition => _enemyDefinition;
    public EnemyController EnemyPrefab => _enemyPrefab;
    public IReadOnlyList<string> SpawnPointIds => _spawnPointIds;
    public string SpawnPointId => _spawnPointId;
    public int Count => _count;
    public float SpawnInterval => _spawnInterval;
    public float InitialDelay => _initialDelay;

    public List<string> GetResolvedSpawnPointIds()
    {
        var result = new List<string>();
        if (_spawnPointIds != null)
        {
            for (int i = 0; i < _spawnPointIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(_spawnPointIds[i]))
                    result.Add(_spawnPointIds[i]);
            }
        }

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(_spawnPointId))
        {
            result.Add(_spawnPointId);
        }

        return result;
    }
}