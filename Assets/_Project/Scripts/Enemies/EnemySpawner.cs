using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Shared")]
    [SerializeField] private EnemyController _enemyPrefab;
    [SerializeField] private Transform _enemiesParent;
    [Header("Level configuration")]
    [SerializeField] private List<EnemySpawnPointBinding> _spawnPointBindings = new List<EnemySpawnPointBinding>();
    [Tooltip("Element 0 is night 1, element 1 is night 2, and so on.")]
    [SerializeField] private List<EnemyWaveDefinition> _waves = new List<EnemyWaveDefinition>();

    private PhaseManager _phases;
    private readonly HashSet<EnemyController> _aliveEnemies = new HashSet<EnemyController>();
    private readonly EnemyWaveProgress _progress = new EnemyWaveProgress();
    private readonly Dictionary<string, Transform> _pointsById = new Dictionary<string, Transform>(StringComparer.Ordinal);
    private Coroutine _spawnRoutine;
    private bool _started;

    public int RemainingSpawns => _progress.RemainingSpawns;
    public int AliveCount => _progress.AliveCount;

    private void Awake()
    {
        if (FindFirstObjectByType<EnemyHoverSelector>() == null)
        {
            gameObject.AddComponent<EnemyHoverSelector>();
        }
    }

    private void Start()
    {
        _started = true;
        Subscribe();
    }

    private void OnEnable()
    {
        if (_started) Subscribe();
    }

    private void Subscribe()
    {
        _phases = PhaseManager.Instance;
        if (_phases == null)
        {
            Debug.LogError("EnemySpawner requires PhaseManager.", this);
            return;
        }
        _phases.OnNightStart += HandleNightStart;
        _phases.OnDayStart += HandleDayStart;
        _phases.OnRunFinished += HandleRunFinished;
        if (_phases.CurrentPhase == PhaseType.Night && !_progress.IsActive)
            HandleNightStart(_phases.CurrentDay);
    }

    private void OnDisable()
    {
        if (_phases != null)
        {
            _phases.OnNightStart -= HandleNightStart;
            _phases.OnDayStart -= HandleDayStart;
            _phases.OnRunFinished -= HandleRunFinished;
        }
        CancelWave();
    }

    private void HandleDayStart(int day) => CancelWave();
    private void HandleRunFinished() => CancelWave();

    private void CancelWave()
    {
        if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
        _progress.Cancel();
        foreach (EnemyController enemy in _aliveEnemies)
        {
            if (enemy == null) continue;
            enemy.OnDied -= HandleEnemyDied;
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }
        _aliveEnemies.Clear();
    }

    private void HandleNightStart(int night)
    {
        if (_progress.IsActive || _aliveEnemies.Count != 0)
        {
            Debug.LogError("Cannot start a second wave while the previous wave is active.", this);
            return;
        }
        if (!TryResolveWave(night, out List<ScheduledGroup> schedule, out int count)) return;
        _progress.Begin(count);
        _spawnRoutine = StartCoroutine(SpawnWave(schedule));
    }

    private void RegisterEnemy(EnemyController enemy)
    {
        _progress.RegisterSpawn();
        _aliveEnemies.Add(enemy);
        enemy.OnDied += HandleEnemyDied;
    }

    private void HandleEnemyDied(EnemyController enemy)
    {
        enemy.OnDied -= HandleEnemyDied;
        if (!_aliveEnemies.Remove(enemy)) return;
        _progress.RegisterDeath();
        TryFinishWave();
    }

    private void TryFinishWave()
    {
        if (_phases != null && _phases.CurrentPhase == PhaseType.Night && _progress.TryComplete())
            _phases.CompleteNight();
    }

    private IEnumerator SpawnWave(List<ScheduledGroup> schedule)
    {
        yield return null;
        foreach (ScheduledGroup group in schedule)
        {
            if (group.InitialDelay > 0f) yield return new WaitForSeconds(group.InitialDelay);
            for (int i = 0; i < group.Count; i++)
            {
                if (!_progress.IsActive || _phases.CurrentPhase != PhaseType.Night) yield break;

                Transform chosenPoint = group.GetRandomInitialSpawnPoint();
                if (chosenPoint == null || !chosenPoint.gameObject.activeInHierarchy ||
                    group.Prefab == null || group.Definition == null ||
                    (_enemiesParent != null && !_enemiesParent.gameObject.activeInHierarchy))
                {
                    _progress.Fail();
                    _spawnRoutine = null;
                    Debug.LogError("Wave spawning stopped: a scheduled spawn reference was removed or disabled. Night will not auto-complete.", this);
                    yield break;
                }

                EnemyController enemy = Instantiate(group.Prefab, chosenPoint.position, chosenPoint.rotation, _enemiesParent);
                RegisterEnemy(enemy);
                enemy.Initialize(group.Definition, group.SpawnPoints, group.DirectionPoints, group.LastPoints);

                if (_progress.RemainingSpawns > 0)
                    yield return new WaitForSeconds(group.SpawnInterval);
            }
        }
        _progress.FinishSpawning();
        _spawnRoutine = null;
        TryFinishWave();
    }

    private bool TryResolveWave(int night, out List<ScheduledGroup> schedule, out int count)
    {
        schedule = new List<ScheduledGroup>();
        count = 0;
        _pointsById.Clear();
        if (_spawnPointBindings == null || _spawnPointBindings.Count == 0)
            return SetupError("Assign this level's Spawn Point Bindings.");
        foreach (EnemySpawnPointBinding binding in _spawnPointBindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Id) || binding.Point == null ||
                !binding.Point.gameObject.activeInHierarchy || _pointsById.ContainsKey(binding.Id))
                return SetupError("Spawn Point Bindings need unique, non-empty IDs and active scene markers.");
            _pointsById.Add(binding.Id, binding.Point);
        }
        if (_waves == null || night < 1 || night > _waves.Count || _waves[night - 1] == null)
            return SetupError($"Assign an Enemy Wave for night {night}; missing waves do not auto-complete.");
        EnemyWaveDefinition wave = _waves[night - 1];
        if (wave.Groups == null || wave.Groups.Count == 0)
            return SetupError($"Wave '{wave.name}' has no spawn groups.");
        if (_enemiesParent != null && !_enemiesParent.gameObject.activeInHierarchy)
            return SetupError("Enemies Parent must be active.");

        foreach (EnemySpawnGroup group in wave.Groups)
        {
            if (group == null || group.Count <= 0 || !IsFinite(group.SpawnInterval) || group.SpawnInterval <= 0f ||
                !IsFinite(group.InitialDelay) || group.InitialDelay < 0f)
                return SetupError($"Wave '{wave.name}' needs positive counts/intervals and non-negative delays.");

            List<string> resolvedIds = group.GetResolvedSpawnPointIds();
            if (resolvedIds.Count == 0)
                return SetupError($"Wave '{wave.name}' needs at least one Spawn Point ID per group.");

            var spawnPoints = new List<Transform>();
            var directionPoints = new List<Transform>();
            var lastPoints = new List<Transform>();

            for (int i = 0; i < resolvedIds.Count; i++)
            {
                string id = resolvedIds[i];
                if (!_pointsById.TryGetValue(id, out Transform pt) || pt == null)
                    return SetupError($"Wave '{wave.name}' references unknown Spawn Point ID '{id}'.");

                if (id.IndexOf("Direction", StringComparison.OrdinalIgnoreCase) >= 0)
                    directionPoints.Add(pt);
                else if (id.IndexOf("Last", StringComparison.OrdinalIgnoreCase) >= 0)
                    lastPoints.Add(pt);
                else
                    spawnPoints.Add(pt);
            }

            if (spawnPoints.Count == 0)
            {
                if (directionPoints.Count > 0) spawnPoints.AddRange(directionPoints);
                else spawnPoints.AddRange(lastPoints);
            }

            EnemyController prefab = group.EnemyPrefab != null ? group.EnemyPrefab : _enemyPrefab;
            EnemyDefinition definition = group.EnemyDefinition;
            if (prefab == null || !prefab.gameObject.activeSelf || !prefab.enabled)
                return SetupError($"Wave '{wave.name}' needs an active enemy prefab with EnemyController.");
            if (definition != null && definition.MovementArchetype == EnemyMovementArchetype.AmphibiousMelee && prefab.GetComponent<NavMeshAgent>() == null)
                return SetupError($"AmphibiousMelee prefab '{prefab.name}' needs a NavMeshAgent.");
            if (!ValidateDefinition(definition)) return false;
            if (group.Count > int.MaxValue - count) return SetupError("Wave enemy count is too large.");
            count += group.Count;
            schedule.Add(new ScheduledGroup(group, prefab, spawnPoints, directionPoints, lastPoints));
        }
        return true;
    }

    private bool ValidateDefinition(EnemyDefinition definition)
    {
        if (definition == null || !IsPositive(definition.MaxHp) || !IsPositive(definition.MoveSpeed) ||
            !IsPositive(definition.AttackInterval) || !IsPositive(definition.AttackRange) ||
            !IsFinite(definition.Damage) || definition.Damage < 0f ||
            (definition.ApproachType != EnemyApproachType.Melee && definition.ApproachType != EnemyApproachType.Ranged))
            return SetupError("Enemy definitions need finite, positive HP, speed, interval and range, plus non-negative damage.");
        if (definition.ApproachType == EnemyApproachType.Ranged &&
            (definition.ProjectilePrefab == null || !definition.ProjectilePrefab.gameObject.activeSelf ||
             !definition.ProjectilePrefab.enabled || !IsPositive(definition.ProjectileSpeed) || !IsPositive(definition.ProjectileLifetime)))
            return SetupError($"Ranged definition '{definition.name}' needs an active EnemyProjectile prefab, positive speed and lifetime.");
        return true;
    }

    private bool SetupError(string message)
    {
        Debug.LogError(message, this);
        return false;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsPositive(float value) => IsFinite(value) && value > 0f;

    private sealed class ScheduledGroup
    {
        public readonly EnemyDefinition Definition;
        public readonly EnemyController Prefab;
        public readonly List<Transform> SpawnPoints;
        public readonly List<Transform> DirectionPoints;
        public readonly List<Transform> LastPoints;
        public readonly int Count;
        public readonly float SpawnInterval;
        public readonly float InitialDelay;

        public ScheduledGroup(EnemySpawnGroup group, EnemyController prefab,
            List<Transform> spawnPoints, List<Transform> directionPoints, List<Transform> lastPoints)
        {
            Definition = group.EnemyDefinition;
            Prefab = prefab;
            SpawnPoints = spawnPoints;
            DirectionPoints = directionPoints;
            LastPoints = lastPoints;
            Count = group.Count;
            SpawnInterval = group.SpawnInterval;
            InitialDelay = group.InitialDelay;
        }

        public Transform GetRandomInitialSpawnPoint()
        {
            if (SpawnPoints == null || SpawnPoints.Count == 0) return null;
            return SpawnPoints[UnityEngine.Random.Range(0, SpawnPoints.Count)];
        }
    }
}