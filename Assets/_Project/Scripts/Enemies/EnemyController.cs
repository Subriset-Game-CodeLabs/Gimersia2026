using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Idle, Moving, Attacking }

public class EnemyController : MonoBehaviour
{
    [Header("Navigation safety")]
    [Tooltip("Small tolerance: never snap offshore spawns across open water.")]
    [SerializeField, Min(0.01f)] private float _attachmentHorizontalTolerance = 0.1f;
    [SerializeField, Min(0.01f)] private float _attachmentVerticalTolerance = 0.35f;
    [SerializeField, Min(0.05f)] private float _repathInterval = 0.5f;
    [SerializeField, Min(0.01f)] private float _destinationSampleRadius = 0.3f;
    [SerializeField, Min(0.01f)] private float _targetVerticalTolerance = 2f;
    [Header("Ranged attack")]
    [SerializeField] private Transform _firePoint;

    public EnemyDefinition Definition { get; private set; }
    public EnemyState State { get; private set; } = EnemyState.Idle;
    public float Hp { get; private set; }
    public bool IsAlive => Hp > 0f;
    public int AttackerCount => _attackers.Count;
    public event Action<EnemyController> OnDied;

    private readonly List<SpiritController> _attackers = new List<SpiritController>();
    private readonly Vector3[] _pathCorners = new Vector3[128];
    private NavMeshAgent _agent;
    private NavMeshPath _path;
    private TreeNode _target;
    private float _attackCooldown;
    private float _nextRepath;
    private float _nextTargetSearch;
    private bool _attached;
    private bool _hasMovePath;
    private bool _deathNotified;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _path = new NavMeshPath();
        // Activation requires our own tight spawn sample, including for island spawns.
        if (_agent != null) _agent.enabled = false;
        else Debug.LogWarning("EnemyController needs a NavMeshAgent for island pursuit.", this);
    }

    public void Initialize(EnemyDefinition definition)
    {
        // A death subscriber cannot resurrect an object already scheduled for destruction.
        if (this == null || _deathNotified) return;
        DisableAgent();
        Definition = definition;
        Hp = definition != null ? Mathf.Max(0f, definition.MaxHp) : 0f;
        _target = null;
        _attackCooldown = 0f;
        _nextRepath = 0f;
        _nextTargetSearch = 0f;
        _attached = false;
        _deathNotified = false;
        _attackers.Clear();
        State = EnemyState.Idle;
        // No combat/death callbacks here: the spawner has not registered us yet.
        if (IsAlive && isActiveAndEnabled) TryAttachAt(transform.position);
    }

    public void AddAttacker(SpiritController spirit)
    {
        if (this != null && spirit != null && IsAlive && isActiveAndEnabled && !_attackers.Contains(spirit))
            _attackers.Add(spirit);
    }

    public void RemoveAttacker(SpiritController spirit)
    {
        _attackers.Remove(spirit);
    }

    public void TakeDamage(float amount)
    {
        if (this == null || !isActiveAndEnabled || !IsAlive || _deathNotified || !IsFinite(amount) || amount <= 0f) return;
        Hp = Mathf.Max(0f, Hp - amount);
        if (IsAlive) return;
        _deathNotified = true;
        _target = null;
        _attackers.Clear();
        State = EnemyState.Idle;
        DisableAgent();
        enabled = false;
        // Schedule destruction before potentially re-entrant wave/phase subscribers.
        Destroy(gameObject);
        OnDied?.Invoke(this);
    }

    private void OnDisable()
    {
        State = EnemyState.Idle;
        _attackers.Clear();
        DisableAgent();
    }

    private void OnDestroy()
    {
        _attackers.Clear();
        OnDied = null;
    }

    private void Update()
    {
        if (Definition == null || !IsAlive || _deathNotified) return;
        _attackCooldown = Mathf.Max(0f, _attackCooldown - Time.deltaTime);
        if (_target == null || !_target.IsTargetable)
        {
            _target = null;
            StopNavigation();
            State = EnemyState.Idle;
            if (Time.time < _nextTargetSearch) return;
            _nextTargetSearch = Time.time + RepathInterval;
            TreeRegistry registry = TreeRegistry.Instance;
            if (registry == null || !registry.TryGetNearest(transform.position, out _target)) return;
            _nextRepath = 0f;
        }
        if (IsWithinAttackRange(transform.position, _target.TargetPoint))
        {
            StopNavigation();
            State = EnemyState.Attacking;
            Attack();
            return;
        }
        if (!_attached)
        {
            MoveOffshore();
            return;
        }
        PursueOnNavMesh();
    }

    private float RepathInterval => Mathf.Max(0.05f, _repathInterval);
    private bool NavigationReady => _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh;
    private NavMeshQueryFilter QueryFilter => new NavMeshQueryFilter
    {
        agentTypeID = _agent.agentTypeID,
        areaMask = _agent.areaMask
    };

    private bool TryAttachAt(Vector3 position)
    {
        if (_agent == null || !isActiveAndEnabled || !IsFinite(position)) return false;
        float horizontal = Mathf.Clamp(_attachmentHorizontalTolerance, 0.01f, 0.25f);
        float vertical = Mathf.Clamp(_attachmentVerticalTolerance, 0.01f, 1f);
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit,
                Mathf.Sqrt(horizontal * horizontal + vertical * vertical), QueryFilter)
            || !IsFinite(hit.position)
            || GroundDistanceSquared(position, hit.position) > horizontal * horizontal
            || Mathf.Abs(position.y - hit.position.y) > vertical)
            return false;
        transform.position = hit.position;
        _agent.enabled = true;
        if (!_agent.isOnNavMesh || !_agent.Warp(hit.position))
        {
            _agent.enabled = false;
            transform.position = position;
            return false;
        }
        _attached = true;
        _hasMovePath = false;
        _agent.autoRepath = false;
        _agent.autoBraking = true;
        _agent.speed = Mathf.Max(0f, Definition.MoveSpeed);
        // Destinations are inside attack range; a large stop distance would stop too early.
        _agent.stoppingDistance = Mathf.Min(0.05f, Mathf.Max(0f, Definition.AttackRange) * 0.01f);
        _agent.isStopped = true;
        _nextRepath = 0f;
        return true;
    }

    private void MoveOffshore()
    {
        if (_agent == null)
        {
            State = EnemyState.Idle;
            return;
        }
        if (TryAttachAt(transform.position))
        {
            PursueOnNavMesh();
            return;
        }
        Vector3 delta = _target.TargetPoint - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        float travel = Mathf.Min(distance, Mathf.Max(0f, Definition.MoveSpeed) * Time.deltaTime);
        if (travel <= 0f || !IsFinite(travel))
        {
            State = EnemyState.Idle;
            return;
        }
        Vector3 direction = delta / distance;
        float spacing = Mathf.Clamp(_attachmentHorizontalTolerance * 0.5f, 0.005f, 0.05f);
        const int maxSubsteps = 256;
        State = EnemyState.Moving;
        transform.rotation = Quaternion.LookRotation(direction);
        // Cap travel rather than stretch steps: high speeds cannot skip a narrow shore.
        for (int i = 0; i < maxSubsteps && travel > 0f; i++)
        {
            float step = Mathf.Min(spacing, travel);
            Vector3 next = transform.position + direction * step;
            if (TryAttachAt(next))
            {
                PursueOnNavMesh();
                return;
            }
            transform.position = next;
            travel -= step;
            if (IsWithinAttackRange(next, _target.TargetPoint)) break;
        }
    }

    private void PursueOnNavMesh()
    {
        // Attachment is one-way: loss of mesh/agent never permits direct movement again.
        if (!NavigationReady)
        {
            State = EnemyState.Idle;
            _hasMovePath = false;
            if (Time.time < _nextRepath) return;
            _nextRepath = Time.time + RepathInterval;
            if (_agent != null)
            {
                _agent.enabled = false;
                if (TryAttachAt(transform.position)) _nextRepath = Time.time + RepathInterval;
            }
            return;
        }
        if (_hasMovePath && (_agent.isPathStale || _agent.pathPending
            || _agent.pathStatus != NavMeshPathStatus.PathComplete || !_agent.hasPath))
            StopNavigation();
        if (Time.time >= _nextRepath)
        {
            _nextRepath = Time.time + RepathInterval;
            if (!TrySetAttackDestination()) StopNavigation();
        }
        State = _hasMovePath ? EnemyState.Moving : EnemyState.Idle;
    }

    private bool TrySetAttackDestination()
    {
        if (!NavigationReady || _target == null || !_target.IsTargetable) return false;
        Vector3 center = _target.TargetPoint;
        float range = Mathf.Max(0f, Definition.AttackRange);
        if (!IsFinite(center) || !IsFinite(range) || range <= 0f) return false;
        Vector3 approach = transform.position - center;
        float startAngle = Mathf.Atan2(approach.z, approach.x);
        const int samplesPerRing = 16;
        const int rings = 3;
        for (int ring = 0; ring < rings; ring++)
        {
            float radius = range * (ring == 0 ? 0.85f : ring == 1 ? 0.5f : 0.98f);
            for (int i = 0; i < samplesPerRing; i++)
            {
                float angle = startAngle + i * (Mathf.PI * 2f / samplesPerRing);
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                        Mathf.Clamp(_destinationSampleRadius, 0.01f, 1f), QueryFilter)
                    || !IsWithinAttackRange(hit.position, center)
                    || !_agent.CalculatePath(hit.position, _path)
                    || _path.status != NavMeshPathStatus.PathComplete)
                    continue;
                int count = _path.GetCornersNonAlloc(_pathCorners);
                // Reject truncated buffers and endpoints projected outside actual attack range.
                if (count <= 0 || count >= _pathCorners.Length
                    || !IsWithinAttackRange(_pathCorners[count - 1], center)) continue;
                if (!_agent.SetPath(_path)) continue;
                _agent.isStopped = false;
                _hasMovePath = true;
                return true;
            }
        }
        return false;
    }

    private bool IsWithinAttackRange(Vector3 position, Vector3 target)
    {
        float range = Mathf.Max(0f, Definition.AttackRange);
        return IsFinite(position) && IsFinite(target) && IsFinite(range)
            && Mathf.Abs(position.y - target.y) <= Mathf.Max(0.01f, _targetVerticalTolerance)
            && GroundDistanceSquared(position, target) <= range * range;
    }

    private void Attack()
    {
        if (_attackCooldown > 0f || _target == null || !_target.IsTargetable) return;
        _attackCooldown = Mathf.Max(0.01f, Definition.AttackInterval);
        if (Definition.ApproachType == EnemyApproachType.Ranged)
        {
            // Missing configuration is harmless; never substitute instant damage for a shot.
            if (Definition.ProjectilePrefab == null) return;
            Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;
            EnemyProjectile projectile = Instantiate(Definition.ProjectilePrefab, origin, transform.rotation);
            projectile.Launch(_target, Definition.Damage, Definition.ProjectileSpeed, Definition.ProjectileLifetime);
            return;
        }
        _target.Data.TakeDamage(Definition.Damage);
    }

    private void StopNavigation()
    {
        _hasMovePath = false;
        if (!NavigationReady) return;
        _agent.isStopped = true;
        if (_agent.hasPath || _agent.pathPending) _agent.ResetPath();
    }

    private void DisableAgent()
    {
        StopNavigation();
        if (_agent != null) _agent.enabled = false;
    }

    private static float GroundDistanceSquared(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return x * x + z * z;
    }

    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
