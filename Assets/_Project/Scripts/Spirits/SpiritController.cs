using System;
using UnityEngine;
using UnityEngine.AI;

public enum SpiritState { Idle, Moving, Interacting, Fighting }


public class SpiritController : MonoBehaviour
{
    // TODO(balance): placeholder move speed.
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _arriveDistance = 0.05f;
    [SerializeField, Range(0.05f, 2f)] private float _destinationSampleRadius = 1f;
    [SerializeField, Min(0.05f)] private float _pursuitRepathInterval = 0.25f;
    [SerializeField] private SpiritCombatSettings _combatSettings;

    private NavMeshAgent _agent;
    private NavMeshPath _path;
    private readonly Vector3[] _pathCorners = new Vector3[128];
    private bool _hasMovePath;
    private bool _reportedInvalidSetup;
    private bool _registeredAsWorker;
    private float _nextPursuitRepath;
    private Vector3 _target;
    private IInteractable _interactable;
    private EnemyController _enemy;
    private Bounds _islandBounds;
    private float _attackCooldown;
    private bool _registeredAsAttacker;

    public SpiritState State { get; private set; } = SpiritState.Idle;

    // Shared destinations need room for all workers; combat still closes to attack range.
    private float ArrivalDistance => State == SpiritState.Fighting
        ? Mathf.Max(0.01f, _arriveDistance)
        : Mathf.Max(Mathf.Max(0.01f, _arriveDistance), _agent.radius * 2f + 0.05f);

    public event Action OnArrived;
    // For the animation task: one place to react to idle, moving and working.
    public event Action<SpiritState> OnStateChanged;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _path = new NavMeshPath();
        if (_agent != null)
        {
            // Every route is validated here, including its island bounds in combat.
            _agent.autoRepath = false;
        }
        // Idle spirits do no per-frame work.
        enabled = false;
    }

    private void OnDestroy()
    {
        Stop();
    }

    private void OnDisable()
    {
        Stop();
    }

    public void MoveTo(Vector3 worldPoint)
    {
        Stop();
        BeginMove(worldPoint);
    }

    public void Interact(IInteractable target)
    {
        if (!IsLiveTarget(target) || !target.CanInteract) return;

        Stop();
        SetTarget(target);
        BeginMove(target.InteractionPoint);
    }

    public void Attack(EnemyController enemy, Bounds islandBounds)
    {
        if (enemy == null || !enemy.IsAlive || _combatSettings == null) return;

        Stop();
        if (!NavigationReady()) return;
        _enemy = enemy;
        _islandBounds = islandBounds;
        _attackCooldown = 0f;
        _nextPursuitRepath = 0f;
        if (!InsideIsland(transform.position))
        {
            Stop();
            return;
        }
        SetState(SpiritState.Fighting);
        enabled = true;
    }

    private void ClearEnemyTarget()
    {
        UnregisterAttacker();
        _enemy = null;
    }

    // The only place the target changes. It unregisters from the old target first,
    // so every exit path (new order, finish, destroy) stays correct.
    // Remove is a no-op if the spirit was never registered (still walking).
    private void SetTarget(IInteractable target)
    {
        if (_registeredAsWorker && IsLiveTarget(_interactable)) _interactable.RemoveWorker(this);
        _registeredAsWorker = false;
        _interactable = target;
    }

    private static bool IsLiveTarget(IInteractable target)
    {
        return target != null && (!(target is UnityEngine.Object obj) || obj != null);
    }

    private void SetState(SpiritState state)
    {
        if (State == state) return;
        State = state;
        OnStateChanged?.Invoke(state);
    }

    private void BeginMove(Vector3 worldPoint)
    {
        if (!TryBeginPath(worldPoint, false))
        {
            Stop();
            return;
        }
        SetState(SpiritState.Moving);
        enabled = true;
    }

    private bool NavigationReady()
    {
        if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
        {
            _reportedInvalidSetup = false;
            return true;
        }
        if (!_reportedInvalidSetup)
        {
            Debug.LogWarning("Spirit requires an enabled NavMeshAgent placed on a baked NavMesh.", this);
            _reportedInvalidSetup = true;
        }
        return false;
    }

    private static bool IsFinite(Vector3 point)
    {
        return !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y)
            && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
    }

    private bool InsideIsland(Vector3 point)
    {
        return IsFinite(point) && point.x >= _islandBounds.min.x && point.x <= _islandBounds.max.x
            && point.z >= _islandBounds.min.z && point.z <= _islandBounds.max.z;
    }

    private bool TryBeginPath(Vector3 worldPoint, bool constrainToIsland)
    {
        if (!NavigationReady() || !IsFinite(worldPoint)) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
        if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit hit,
            Mathf.Clamp(_destinationSampleRadius, 0.05f, 2f), filter)
            || !_agent.CalculatePath(hit.position, _path)
            || _path.status != NavMeshPathStatus.PathComplete)
            return false;

        if (constrainToIsland)
        {
            if (!InsideIsland(transform.position) || !InsideIsland(hit.position)) return false;
            int count = _path.GetCornersNonAlloc(_pathCorners);
            // Reject truncated paths rather than trust unchecked sections.
            if (count == _pathCorners.Length) return false;
            for (int i = 0; i < count; i++)
                if (!InsideIsland(_pathCorners[i])) return false;
        }

        _agent.speed = Mathf.Max(0f, _moveSpeed);
        _agent.stoppingDistance = ArrivalDistance;
        _agent.autoRepath = false;
        if (!_agent.SetPath(_path)) return false;
        _agent.isStopped = false;
        _target = hit.position;
        _hasMovePath = true;
        return true;
    }

    private bool HasArrived()
    {
        float tolerance = ArrivalDistance;
        // Agent base offset / visual height must not prevent ground-plane arrival.
        Vector3 toTarget = transform.position - _target;
        toTarget.y = 0f;
        return toTarget.sqrMagnitude <= tolerance * tolerance
            && (!_agent.hasPath || _agent.remainingDistance <= tolerance);
    }

    private bool PathIsValid()
    {
        return _hasMovePath && !_agent.isPathStale
            && _agent.pathStatus == NavMeshPathStatus.PathComplete
            && (_agent.hasPath || HasArrived());
    }

    private void StopNavigation()
    {
        _hasMovePath = false;
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    private void Update()
    {
        switch (State)
        {
            case SpiritState.Moving:
                UpdateMoving();
                break;
            case SpiritState.Interacting:
                UpdateInteracting();
                break;
            case SpiritState.Fighting:
                UpdateFighting();
                break;
        }
    }

    private void UpdateMoving()
    {
        if (!NavigationReady() || (_interactable != null
            && (!IsLiveTarget(_interactable) || !_interactable.CanInteract)))
        {
            Stop();
            return;
        }
        if (_agent.pathPending) return;
        if (!PathIsValid())
        {
            Stop();
            return;
        }
        if (!HasArrived()) return;

        StopNavigation();
        IInteractable arrivedTarget = _interactable;
        OnArrived?.Invoke();
        // An arrival listener may issue another command.
        if (State != SpiritState.Moving || _hasMovePath || _interactable != arrivedTarget) return;

        if (IsLiveTarget(_interactable) && _interactable.CanInteract)
        {
            // Register only on arrival, so walking spirits don't count for the multiplier.
            _interactable.AddWorker(this);
            _registeredAsWorker = true;
            SetState(SpiritState.Interacting);
        }
        else
        {
            Stop();
        }
    }

    private void UpdateInteracting()
    {
        // The target may finish while we work (or because another spirit finished it).
        if (!NavigationReady() || !IsLiveTarget(_interactable) || !_interactable.CanInteract)
        {
            Stop();
            return;
        }

        _interactable.Interact(this, Time.deltaTime);
    }

    private void UpdateFighting()
    {
        if (!NavigationReady() || _enemy == null || !_enemy.IsAlive || _combatSettings == null
            || !InsideIsland(transform.position))
        {
            Stop();
            return;
        }

        Vector3 toEnemy = _enemy.transform.position - transform.position;
        toEnemy.y = 0f;
        float distance = toEnemy.magnitude;
        float range = _combatSettings.AttackRange;
        if (distance > range)
        {
            UnregisterAttacker();
            if (_hasMovePath && !_agent.pathPending && !PathIsValid())
            {
                Stop();
                return;
            }
            if (!_hasMovePath || Time.time >= _nextPursuitRepath)
            {
                // The NavMesh defines the walkable island; bounds also exclude external meshes.
                Vector3 destination = _enemy.transform.position;
                destination.x = Mathf.Clamp(destination.x, _islandBounds.min.x, _islandBounds.max.x);
                destination.z = Mathf.Clamp(destination.z, _islandBounds.min.z, _islandBounds.max.z);
                destination.y = transform.position.y;
                if (!TryBeginPath(destination, true))
                {
                    Stop();
                    return;
                }
                Vector3 toEndpoint = _target - _enemy.transform.position;
                toEndpoint.y = 0f;
                if (toEndpoint.sqrMagnitude > range * range)
                {
                    Stop();
                    return;
                }
                _nextPursuitRepath = Time.time + Mathf.Max(0.05f, _pursuitRepathInterval);
            }
            // A reachable shore point may still be too far from a ranged enemy.
            if (!_agent.pathPending && HasArrived()) Stop();
            return;
        }

        StopNavigation();
        if (!_registeredAsAttacker)
        {
            _enemy.AddAttacker(this);
            _registeredAsAttacker = true;
        }
        if (distance > 0.001f)
            transform.rotation = Quaternion.LookRotation(toEnemy / distance);
        _attackCooldown -= Time.deltaTime;
        if (_attackCooldown > 0f) return;

        TreeRegistry registry = TreeRegistry.Instance;
        ElementSettings elements = registry != null ? registry.Settings : null;
        ElementType counter = elements != null
            ? elements.GetCounterTo(_enemy.Definition.Element)
            : ElementType.None;
        float damage = _combatSettings.Damage;
        float attackSpeed = 1f;
        if (elements != null)
        {
            damage *= elements.GetDamageMultiplier(counter, _enemy.Definition.Element);
            if (counter != ElementType.None)
            {
                int power = registry.GetTotalPower(counter);
                damage *= elements.GetDamageBonusMultiplier(power);
                attackSpeed = registry.GetAttackSpeedMultiplier(counter);
            }
        }

        TeamworkSettings teamwork = _combatSettings.TeamworkSettings;
        if (teamwork != null)
        {
            int count = Mathf.Max(1, _enemy.AttackerCount);
            damage *= teamwork.GetMultiplier(count) / count;
        }
        _enemy.TakeDamage(damage);
        _attackCooldown = _combatSettings.AttackInterval / Mathf.Max(0.01f, attackSpeed);
    }

    private void UnregisterAttacker()
    {
        if (!_registeredAsAttacker) return;
        if (_enemy != null) _enemy.RemoveAttacker(this);
        _registeredAsAttacker = false;
    }

    private void Stop()
    {
        StopNavigation();
        SetTarget(null);
        ClearEnemyTarget();
        SetState(SpiritState.Idle);
        enabled = false;
    }
}
