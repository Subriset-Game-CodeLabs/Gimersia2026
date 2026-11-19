using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Shared")]
    [SerializeField] private EnemyController _enemyPrefab;
    [SerializeField] private Transform _enemiesParent;
    [Header("Melee")]
    [SerializeField] private EnemyDefinition _definition;
    [SerializeField] private Transform[] _spawnPoints;
    [Header("Ranged test route")]
    [SerializeField] private EnemyDefinition _rangedDefinition;
    [SerializeField] private Transform _rangedSpawnPoint;
    [SerializeField] private Transform _rangedStopPoint;

    private PhaseManager _phases;
    private readonly HashSet<EnemyController> _aliveEnemies = new HashSet<EnemyController>();
    private bool _spawningWave;
    private bool _waveActive;

    private void Start()
    {
        // Subscribe in Start: the phase manager begins day 1 on its first Update.
        _phases = PhaseManager.Instance;
        _phases.OnNightStart += HandleNightStart;
    }

    private void OnDestroy()
    {
        if (_phases != null) _phases.OnNightStart -= HandleNightStart;
        foreach (EnemyController enemy in _aliveEnemies)
            if (enemy != null) enemy.OnDied -= HandleEnemyDied;
        _aliveEnemies.Clear();
    }

    private void HandleNightStart(int night)
    {
        // TODO(balance): one of each type per night is only a test wave.
        _waveActive = true;
        _spawningWave = true;
        SpawnMelee();
        SpawnRanged();
        _spawningWave = false;
        if (_aliveEnemies.Count == 0)
            Debug.LogError("Night cannot finish: no enemies spawned. Check EnemySpawner setup.", this);
    }

    private void RegisterEnemy(EnemyController enemy)
    {
        _aliveEnemies.Add(enemy);
        enemy.OnDied += HandleEnemyDied;
    }

    private void HandleEnemyDied(EnemyController enemy)
    {
        enemy.OnDied -= HandleEnemyDied;
        if (!_aliveEnemies.Remove(enemy) || !_waveActive || _spawningWave ||
            _aliveEnemies.Count != 0)
            return;

        _waveActive = false;
        _phases.CompleteNight();
    }

    private void SpawnMelee()
    {
        if (_enemyPrefab == null || _definition == null ||
            _spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogError("EnemySpawner is missing melee setup.", this);
            return;
        }
        Transform point = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
        if (point == null)
        {
            Debug.LogError("EnemySpawner has an empty melee spawn-point entry.", this);
            return;
        }
        EnemyController enemy = Instantiate(
            _enemyPrefab, point.position, Quaternion.identity, _enemiesParent);
        enemy.Initialize(_definition);
        RegisterEnemy(enemy);
    }
    private void SpawnRanged()
    {
        if (_enemyPrefab == null || _rangedDefinition == null ||
            _rangedSpawnPoint == null || _rangedStopPoint == null)
        {
            Debug.LogError("EnemySpawner is missing ranged setup.", this);
            return;
        }
        EnemyController enemy = Instantiate(
            _enemyPrefab,
            _rangedSpawnPoint.position,
            Quaternion.identity,
            _enemiesParent);
        enemy.Initialize(_rangedDefinition, _rangedStopPoint.position);
        RegisterEnemy(enemy);
    }
}
