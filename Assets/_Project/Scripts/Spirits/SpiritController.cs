using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum SpiritState { Idle, Moving, Interacting, Fighting }

public class SpiritController : MonoBehaviour
{
    private static readonly List<SpiritController> ActiveSpirits = new List<SpiritController>();
    public static IReadOnlyList<SpiritController> RegisteredSpirits => ActiveSpirits;
    private readonly SpiritItemStatus _itemStatus = new SpiritItemStatus();
    public bool CanReceiveTeamItem => gameObject.activeInHierarchy;
    public bool CanReceiveTeamHaste => CanReceiveTeamItem;
    public SpiritItemStatus ItemStatus => _itemStatus;

    public bool TryApplyHaste(float duration, float moveMultiplier, float attackMultiplier)
    {
        if (!CanReceiveTeamHaste || !_itemStatus.TryApplyHaste(duration, moveMultiplier, attackMultiplier)) return false;
        RefreshMovementSpeed();
        enabled = true;
        return true;
    }

    public void ClearItemStatus()
    {
        _itemStatus.Clear();
        RefreshMovementSpeed();
    }

    public bool TryApplyEmpowerment(float duration, float damageMultiplier)
    {
        if (!CanReceiveTeamItem || !_itemStatus.TryApplyEmpowerment(duration, damageMultiplier)) return false;
        enabled = true;
        return true;
    }

    private void RefreshMovementSpeed()
    {
        if (_agent != null) _agent.speed = Mathf.Max(0f, _moveSpeed) * _itemStatus.MoveSpeedMultiplier;
    }

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _arriveDistance = 0.05f;
    [SerializeField, Range(0.05f, 2f)] private float _destinationSampleRadius = 1f;
    [SerializeField, Min(0.05f)] private float _pursuitRepathInterval = 0.3f;
    [SerializeField] private float _turnSpeed = 12f;

    [Header("Tree Interaction")]
    [Tooltip("Jika aktif, Spirit mendekati pohon dari arah datangnya dan langsung berinteraksi tanpa memutar ke titik kiri pohon.")]
    [SerializeField] private bool _interactTreeFromApproachDirection = true;
    [Tooltip("Jarak berhenti Spirit dari titik pusat pohon saat menumbuhkan / merawat pohon.")]
    [SerializeField, Min(0.2f)] private float _treeInteractRadius = 0.85f;

    [Header("Element Power Milestone VFX")]
    [Tooltip("Batas minimal power elemen untuk memunculkan VFX (dapat diubah bebas di Inspector).")]
    [SerializeField, Min(1)] private int _powerMilestoneThreshold = 50;
    [Tooltip("Prefab VFX dari folder Project ATAU child GameObject VFX di dalam Spirit.")]
    [SerializeField] private GameObject _powerMilestoneVfxPrefab;
    [Tooltip("Opsional: titik kemunculan VFX pada Spirit. Otomatis memakai posisi Spirit jika kosong.")]
    [SerializeField] private Transform _powerVfxSpawnPoint;
    [Tooltip("Jika aktif, VFX menempel dan ikut bergerak bersama Spirit.")]
    [SerializeField] private bool _attachVfxToSpirit = true;
    [Tooltip("Centang jika VFX berupa aura permanen yang harus hilang apabila pohon diserang dan power turun di bawah batas.")]
    [SerializeField] private bool _destroyVfxWhenBelowThreshold = false;

    [Header("Combat & Projectile")]
    [SerializeField] private SpiritCombatSettings _combatSettings;
    [Tooltip("Temporary damage snapshot diagnostics, at most once per scaled second per spirit. Disable after testing.")]
    [SerializeField] private bool _logItemDamageSnapshots;
    private float _nextItemDamageLogTime;
    [Tooltip("Multiplier damage tambahan ketika Special Attack terpilih.")]
    [SerializeField, Min(1f)] private float _specialAttackDamageMultiplier = 1.6f;
    [Tooltip("Skala ukuran prefab projectile 3D sphere saat Special Attack.")]
    [SerializeField, Min(1f)] private float _specialProjectileScale = 1.65f;
    [Tooltip("Jarak antar Spirit saat berjejer di bibir pantai ketika 2-3 Spirit menyerang bersamaan.")]
    [SerializeField, Min(0.2f)] private float _shoreFormationSpacing = 0.85f;

    private NavMeshAgent _agent;
    private NavMeshPath _path;
    private SpiritView _view;
    private SpiritAnimator _animator;
    private StageBuffState _buffs;

    public void InitializeBuffs(StageBuffState buffs)
    {
        ClearItemStatus();
        _buffs = buffs;
        ResetPowerMilestoneState();
    }

    private readonly Vector3[] _pathCorners = new Vector3[128];
    private bool _hasMovePath;
    private bool _reportedInvalidSetup;
    private bool _registeredAsWorker;
    private float _nextPursuitRepath;
    private Vector3 _target;
    private IInteractable _interactable;
    private bool _useRadialTreeInteraction;
    private Vector3 _radialInteractCenter;
    private EnemyController _enemy;
    private Vector3 _lastKnownEnemyPos;
    private Bounds _islandBounds;
    private float _attackCooldown;
    private bool _registeredAsAttacker;

    // Monitor milestone power elemen
    private bool _hasStarted;
    private Coroutine _powerMonitorCoroutine;
    private bool _hasTriggeredPowerMilestone;
    private GameObject _activeMilestoneVfxInstance;

    // State mesin bertarung
    private int _formationSlotIndex;
    private int _formationSlotCount = 1;
    private bool _hasReachedShoreWaitSpot;
    private Vector3 _lockedShoreSpot;
    private bool _wasTargetHiddenUnderwater;
    private bool _hasEngagedCurrentTarget;
    private float _offIslandOutOfRangeTimer;

    private bool _hasPendingShot;
    private bool _isSpecialPendingShot;
    private float _pendingDamage;
    private float _pendingProjectileScale;
    private EnemyController _pendingEnemy;

    public SpiritCombatSettings CombatSettings => _combatSettings;
    public SpiritState State { get; private set; } = SpiritState.Idle;
    public bool IsChasingEnemy { get; private set; }
    public ElementType Element => _view != null ? _view.Element : ElementType.None;
    public int CommandId { get; private set; }

    private float ArrivalDistance => State == SpiritState.Fighting
        ? Mathf.Max(0.05f, _arriveDistance)
        : Mathf.Max(Mathf.Max(0.01f, _arriveDistance), _agent.radius * 2f + 0.05f);

    public event Action OnArrived;
    public event Action<SpiritState> OnStateChanged;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _view = GetComponent<SpiritView>();
        _animator = GetComponent<SpiritAnimator>();
        _path = new NavMeshPath();

        if (_agent != null)
            _agent.autoRepath = false;

        if (!ActiveSpirits.Contains(this))
            ActiveSpirits.Add(this);

        // Jika VFX dipasang sebagai child di dalam Spirit, pastikan tersembunyi di awal game
        if (_powerMilestoneVfxPrefab != null && _powerMilestoneVfxPrefab.transform.IsChildOf(transform))
        {
            _powerMilestoneVfxPrefab.SetActive(false);
        }

        enabled = false;
    }

    private void Start()
    {
        _hasStarted = true;
        StartPowerMonitor();
    }

    private void OnEnable()
    {
        if (_hasStarted)
            StartPowerMonitor();
    }

    private void OnDestroy()
    {
        ClearItemStatus();
        ActiveSpirits.Remove(this);
        Stop();
    }

    private void OnDisable()
    {
        if (_powerMonitorCoroutine != null)
        {
            StopCoroutine(_powerMonitorCoroutine);
            _powerMonitorCoroutine = null;
        }

        if (!gameObject.activeInHierarchy) ClearItemStatus();
        Stop();
    }

    private void StartPowerMonitor()
    {
        if (_powerMonitorCoroutine != null) return;
        _powerMonitorCoroutine = StartCoroutine(MonitorElementPowerRoutine());
    }

    private IEnumerator MonitorElementPowerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.2f);
        while (true)
        {
            EvaluateElementPowerMilestone();
            yield return wait;
        }
    }

    private void EvaluateElementPowerMilestone()
    {
        TreeRegistry registry = TreeRegistry.Instance;
        ElementType spiritElement = Element;
        if (registry == null || spiritElement == ElementType.None) return;

        int currentPower = registry.GetTotalPower(spiritElement);
        int threshold = Mathf.Max(1, _powerMilestoneThreshold);

        if (currentPower >= threshold)
        {
            if (!_hasTriggeredPowerMilestone)
            {
                _hasTriggeredPowerMilestone = true;
                SpawnPowerMilestoneVfx();
            }
        }
        else if (_hasTriggeredPowerMilestone)
        {
            _hasTriggeredPowerMilestone = false;
            if (_destroyVfxWhenBelowThreshold)
            {
                HideOrDestroyMilestoneVfx();
            }
        }
    }

    private void SpawnPowerMilestoneVfx()
    {
        if (_powerMilestoneVfxPrefab == null) return;

        // Mode 1: VFX adalah child GameObject yang sudah ditempel di dalam Spirit
        if (_powerMilestoneVfxPrefab.transform.IsChildOf(transform))
        {
            _powerMilestoneVfxPrefab.SetActive(true);
            ParticleSystem childPs = _powerMilestoneVfxPrefab.GetComponentInChildren<ParticleSystem>(true);
            if (childPs != null)
            {
                childPs.Clear(true);
                childPs.Play(true);
            }
            return;
        }

        // Mode 2: VFX adalah Prefab asset dari folder Project
        Transform anchor = _powerVfxSpawnPoint != null ? _powerVfxSpawnPoint : transform;
        _activeMilestoneVfxInstance = _attachVfxToSpirit
            ? Instantiate(_powerMilestoneVfxPrefab, anchor.position, anchor.rotation, anchor)
            : Instantiate(_powerMilestoneVfxPrefab, anchor.position, anchor.rotation);

        if (_activeMilestoneVfxInstance != null)
        {
            _activeMilestoneVfxInstance.SetActive(true);
            ParticleSystem ps = _activeMilestoneVfxInstance.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null)
                ps.Play(true);
        }
    }

    private void HideOrDestroyMilestoneVfx()
    {
        if (_powerMilestoneVfxPrefab != null && _powerMilestoneVfxPrefab.transform.IsChildOf(transform))
        {
            _powerMilestoneVfxPrefab.SetActive(false);
            return;
        }

        if (_activeMilestoneVfxInstance != null)
        {
            Destroy(_activeMilestoneVfxInstance);
            _activeMilestoneVfxInstance = null;
        }
    }

    private void ResetPowerMilestoneState()
    {
        _hasTriggeredPowerMilestone = false;
        HideOrDestroyMilestoneVfx();
    }

    public void MoveTo(Vector3 worldPoint)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        CommandId++;
        if (_animator != null) _animator.NotifyPlayerInteraction();
        Stop();
        BeginMove(worldPoint);
    }

    public void Interact(IInteractable target)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (!IsLiveTarget(target) || !target.CanInteract) return;

        CommandId++;
        if (_animator != null) _animator.NotifyPlayerInteraction();
        Stop();
        SetTarget(target);

        if (_interactTreeFromApproachDirection && TryGetTreeCenter(target, out Vector3 treeCenter))
        {
            _useRadialTreeInteraction = true;
            _radialInteractCenter = treeCenter;

            float interactRadius = Mathf.Max(0.2f, _treeInteractRadius);
            float currentFlatDist = FlatDistance(transform.position, treeCenter);

            if (currentFlatDist <= interactRadius + 0.08f && NavigationReady())
            {
                StartInteractingWithCurrentTarget();
                return;
            }

            Vector3 fromTree = transform.position - treeCenter;
            fromTree.y = 0f;
            Vector3 approachDir = fromTree.sqrMagnitude > 0.0001f ? fromTree.normalized : Vector3.back;
            Vector3 desiredApproachPoint = treeCenter + approachDir * interactRadius;

            if (TryBeginPath(desiredApproachPoint, false))
            {
                SetState(SpiritState.Moving);
                enabled = true;
                return;
            }

            float fallbackRadius = Mathf.Max(_destinationSampleRadius, interactRadius + 0.6f);
            if (TryBeginPath(treeCenter, false, fallbackRadius))
            {
                SetState(SpiritState.Moving);
                enabled = true;
                return;
            }
        }

        _useRadialTreeInteraction = false;
        BeginMove(target.InteractionPoint);
    }

    private static bool TryGetTreeCenter(IInteractable target, out Vector3 treeCenter)
    {
        if (target is Component comp)
        {
            TreeNode tree = comp.GetComponentInParent<TreeNode>();
            if (tree != null)
            {
                treeCenter = tree.TargetPoint;
                return true;
            }
        }

        treeCenter = Vector3.zero;
        return false;
    }

    private void StartInteractingWithCurrentTarget()
    {
        StopNavigation();
        IInteractable arrivedTarget = _interactable;
        OnArrived?.Invoke();
        if (_hasMovePath || _interactable != arrivedTarget) return;

        if (IsLiveTarget(_interactable) && _interactable.CanInteract)
        {
            _interactable.AddWorker(this);
            _registeredAsWorker = true;
            SetState(SpiritState.Interacting);
            enabled = true;
        }
        else
        {
            Stop();
        }
    }

    public void Attack(EnemyController enemy, Bounds islandBounds, int formationSlotIndex = 0, int formationSlotCount = 1)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (_combatSettings == null || enemy == null || !enemy.IsTargetableBySpirit)
            return;

        CommandId++;
        if (_animator != null) _animator.NotifyPlayerInteraction();
        Stop();
        if (!NavigationReady()) return;

        _enemy = enemy;
        _lastKnownEnemyPos = enemy.transform.position;
        _islandBounds = islandBounds;
        _attackCooldown = 0f;
        _nextPursuitRepath = 0f;
        _hasReachedShoreWaitSpot = false;
        _wasTargetHiddenUnderwater = false;
        _hasEngagedCurrentTarget = false;
        _offIslandOutOfRangeTimer = 0f;

        if (formationSlotCount <= 1)
        {
            int existingAttackers = CountOtherSpiritsTargeting(enemy);
            _formationSlotIndex = existingAttackers;
            _formationSlotCount = existingAttackers + 1;
        }
        else
        {
            _formationSlotIndex = Mathf.Clamp(formationSlotIndex, 0, formationSlotCount - 1);
            _formationSlotCount = formationSlotCount;
        }

        SetState(SpiritState.Fighting);
        enabled = true;
    }

    private void ClearEnemyTarget()
    {
        UnregisterAttacker();
        _enemy = null;
        IsChasingEnemy = false;
        _hasReachedShoreWaitSpot = false;
        _wasTargetHiddenUnderwater = false;
        _hasEngagedCurrentTarget = false;
        _offIslandOutOfRangeTimer = 0f;
        ClearPendingShot();
    }

    private void ClearPendingShot()
    {
        _hasPendingShot = false;
        _isSpecialPendingShot = false;
        _pendingEnemy = null;
        _pendingDamage = 0f;
        _pendingProjectileScale = 1f;
    }

    private void SetTarget(IInteractable target)
    {
        if (_registeredAsWorker && IsLiveTarget(_interactable)) _interactable.RemoveWorker(this);
        _registeredAsWorker = false;
        _interactable = target;
        if (target == null)
            _useRadialTreeInteraction = false;
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
        if (!IsFinite(point)) return false;
        if (_islandBounds.size.sqrMagnitude > 0.01f)
        {
            return point.x >= _islandBounds.min.x && point.x <= _islandBounds.max.x
                && point.z >= _islandBounds.min.z && point.z <= _islandBounds.max.z;
        }
        return true;
    }

    private bool TryBeginPath(Vector3 worldPoint, bool constrainToIsland, float customSampleRadius = -1f)
    {
        if (!NavigationReady() || !IsFinite(worldPoint)) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
        float sampleRadius = customSampleRadius > 0f
            ? customSampleRadius
            : Mathf.Clamp(_destinationSampleRadius, 0.05f, 2f);

        if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, sampleRadius, filter)
            || !_agent.CalculatePath(hit.position, _path)
            || _path.status != NavMeshPathStatus.PathComplete)
            return false;

        if (constrainToIsland)
        {
            if (!InsideIsland(transform.position) || !InsideIsland(hit.position)) return false;
            int count = _path.GetCornersNonAlloc(_pathCorners);
            if (count == _pathCorners.Length) return false;
            for (int i = 0; i < count; i++)
                if (!InsideIsland(_pathCorners[i])) return false;
        }

        if (State == SpiritState.Fighting && FlatDistance(transform.position, hit.position) <= 0.18f)
        {
            StopNavigation();
            return true;
        }

        RefreshMovementSpeed();
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
        if (_useRadialTreeInteraction)
        {
            float interactReach = Mathf.Max(0.2f, _treeInteractRadius) + ArrivalDistance;
            if (FlatDistance(transform.position, _radialInteractCenter) <= interactReach)
                return true;
        }

        float tolerance = ArrivalDistance;
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
        if (_agent.hasPath || _agent.pathPending) _agent.ResetPath();
    }

    private void Update()
    {
        RefreshMovementSpeed();
        if (State == SpiritState.Idle && !_itemStatus.IsActive) enabled = false;
        if (Time.timeScale <= 0f) return;

        if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh &&
            !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.25f)
        {
            AudioManager.Instance.PlaySpiritMoveStep(transform.position);
        }

        if (_hasPendingShot && (_animator == null || !_animator.IsAttackInProgress))
        {
            TriggerAnimationProjectileRelease();
        }

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

        StartInteractingWithCurrentTarget();
    }

    private void UpdateInteracting()
    {
        if (!NavigationReady() || !IsLiveTarget(_interactable) || !_interactable.CanInteract)
        {
            Stop();
            return;
        }

        Vector3 lookTarget = _useRadialTreeInteraction
            ? _radialInteractCenter
            : ((_interactable is Component comp)
                ? comp.transform.position
                : _interactable.InteractionPoint);
        FaceDirection(lookTarget - transform.position);

        _interactable.Interact(this, Time.deltaTime);
    }

    private float GetEffectiveAttackRange(EnemyController targetEnemy)
    {
        if (_combatSettings == null || targetEnemy == null) return 0f;
        return targetEnemy.IsOffIslandOrFlying
            ? _combatSettings.SeaEnemyTargetableRadiusFromIsland
            : _combatSettings.AttackRange;
    }

    private void UpdateFighting()
    {
        if (!NavigationReady() || _combatSettings == null)
        {
            Stop();
            return;
        }

        if (_enemy == null || !_enemy.IsAlive)
        {
            if (!TryAutoChainEnemyInRange())
            {
                Stop();
                return;
            }
        }

        if (_enemy.IsHiddenUnderwater)
        {
            _wasTargetHiddenUnderwater = true;
            UnregisterAttacker();
            ClearPendingShot();
            MoveToOrWaitAtShoreEdge(_lastKnownEnemyPos);
            return;
        }

        if (_wasTargetHiddenUnderwater)
        {
            _wasTargetHiddenUnderwater = false;
            float emergeDist = FlatDistance(transform.position, _enemy.transform.position);
            float emergeRange = GetEffectiveAttackRange(_enemy);

            if (emergeDist > emergeRange)
            {
                if (TryAutoChainEnemyInRange())
                {
                    return;
                }

                _hasReachedShoreWaitSpot = false;
                _hasEngagedCurrentTarget = false;
                _offIslandOutOfRangeTimer = 0f;
            }
        }

        _lastKnownEnemyPos = _enemy.transform.position;

        Vector3 toEnemy = _enemy.transform.position - transform.position;
        toEnemy.y = 0f;
        float distance = toEnemy.magnitude;
        float effectiveRange = GetEffectiveAttackRange(_enemy);

        if (_enemy.IsWormAtLastPoint)
        {
            if (!_hasReachedShoreWaitSpot)
            {
                UnregisterAttacker();
                MoveToOrWaitAtShoreEdge(_enemy.transform.position);
                if (!_hasReachedShoreWaitSpot)
                    return;
            }

            ExecuteRangedAttackAtCurrentPosition(toEnemy, distance);
            return;
        }

        if (distance <= effectiveRange)
        {
            ExecuteRangedAttackAtCurrentPosition(toEnemy, distance);
            return;
        }

        UnregisterAttacker();

        if (_enemy.IsOffIslandOrFlying)
        {
            if (_hasEngagedCurrentTarget)
            {
                if (_enemy.IsWorm)
                {
                    _hasReachedShoreWaitSpot = false;
                    _hasEngagedCurrentTarget = false;
                }
                else
                {
                    _offIslandOutOfRangeTimer += Time.deltaTime;
                    if (_offIslandOutOfRangeTimer < _combatSettings.OffIslandOutOfRangeChaseDelay)
                    {
                        IsChasingEnemy = false;
                        StopNavigation();
                        if (distance > 0.001f) FaceDirection(toEnemy);
                        return;
                    }

                    _hasReachedShoreWaitSpot = false;
                    _hasEngagedCurrentTarget = false;
                    _offIslandOutOfRangeTimer = 0f;
                }
            }

            MoveToOrWaitAtShoreEdge(_enemy.transform.position);
            return;
        }

        if (_hasEngagedCurrentTarget && TryAutoChainEnemyInRange())
        {
            return;
        }

        PursueOnIsland(_enemy.transform.position);
    }

    private void ExecuteRangedAttackAtCurrentPosition(Vector3 toEnemy, float distance)
    {
        _hasEngagedCurrentTarget = true;
        _offIslandOutOfRangeTimer = 0f;
        IsChasingEnemy = false;
        StopNavigation();

        if (!_registeredAsAttacker)
        {
            _enemy.AddAttacker(this);
            _registeredAsAttacker = true;
        }

        if (distance > 0.001f)
            FaceDirection(toEnemy);

        if (_animator != null && _animator.IsAttackInProgress) return;

        _attackCooldown -= Time.deltaTime;
        if (_attackCooldown > 0f) return;

        BeginAttackSwing();
    }

    private void PursueOnIsland(Vector3 enemyWorldPos)
    {
        _hasReachedShoreWaitSpot = false;
        Vector3 queryPos = new Vector3(enemyWorldPos.x, transform.position.y, enemyWorldPos.z);

        var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
        if (NavMesh.SamplePosition(queryPos, out NavMeshHit hit, 12f, filter) &&
            FlatDistance(transform.position, hit.position) <= 0.2f)
        {
            IsChasingEnemy = false;
            StopNavigation();
            FaceDirection(enemyWorldPos - transform.position);
            return;
        }

        IsChasingEnemy = true;
        if (!_hasMovePath || Time.time >= _nextPursuitRepath)
        {
            _nextPursuitRepath = Time.time + Mathf.Max(0.05f, _pursuitRepathInterval);
            if (!TryBeginPath(queryPos, false, 12f))
            {
                Stop();
            }
        }
    }

    private void MoveToOrWaitAtShoreEdge(Vector3 referenceEnemyPos)
    {
        if (!TryResolveShoreStandOffPoint(_enemy, out Vector3 desiredShoreSpot))
        {
            IsChasingEnemy = false;
            StopNavigation();
            FaceDirection(referenceEnemyPos - transform.position);
            return;
        }

        if (_hasReachedShoreWaitSpot)
        {
            IsChasingEnemy = false;
            StopNavigation();
            FaceDirection(referenceEnemyPos - transform.position);
            return;
        }

        float distToSpot = FlatDistance(transform.position, desiredShoreSpot);
        float arriveThreshold = Mathf.Max(0.32f, _agent.radius * 2.4f);
        bool agentFinished = _hasMovePath && !_agent.pathPending && (!_agent.hasPath || _agent.remainingDistance <= 0.12f);

        if (distToSpot <= arriveThreshold || agentFinished)
        {
            _hasReachedShoreWaitSpot = true;
            _lockedShoreSpot = transform.position;
            IsChasingEnemy = false;
            StopNavigation();
            FaceDirection(referenceEnemyPos - transform.position);
            return;
        }

        IsChasingEnemy = true;
        if (!_hasMovePath || Time.time >= _nextPursuitRepath)
        {
            _nextPursuitRepath = Time.time + Mathf.Max(0.05f, _pursuitRepathInterval);
            if (!TryBeginPath(desiredShoreSpot, false, 2.5f))
            {
                _hasReachedShoreWaitSpot = true;
                _lockedShoreSpot = transform.position;
                IsChasingEnemy = false;
                StopNavigation();
            }
        }
    }

    private bool TryResolveShoreStandOffPoint(EnemyController offIslandEnemy, out Vector3 standOffPoint)
    {
        standOffPoint = transform.position;
        if (offIslandEnemy == null || !offIslandEnemy.TryGetNearestIslandShorePoint(out Vector3 shoreEdge))
            return false;

        Vector3 inwardDir = shoreEdge - offIslandEnemy.transform.position;
        inwardDir.y = 0f;
        inwardDir = inwardDir.sqrMagnitude > 0.0001f ? inwardDir.normalized : Vector3.zero;

        float standOff = _combatSettings.ShoreStandOffDistance + (_agent != null ? _agent.radius * 0.5f : 0.1f);
        Vector3 baseSpot = shoreEdge + inwardDir * standOff;

        Vector3 lateral = Vector3.Cross(Vector3.up, inwardDir);
        float centeredOffset = (_formationSlotIndex - (_formationSlotCount - 1) * 0.5f) * _shoreFormationSpacing;
        Vector3 desiredPoint = baseSpot + lateral * centeredOffset;

        var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
        if (NavMesh.SamplePosition(desiredPoint, out NavMeshHit hit, 1.5f, filter))
        {
            standOffPoint = hit.position;
            return true;
        }

        if (NavMesh.SamplePosition(baseSpot, out NavMeshHit baseHit, 1.5f, filter))
        {
            standOffPoint = baseHit.position;
            return true;
        }

        return false;
    }

    private bool TryAutoChainEnemyInRange()
    {
        UnregisterAttacker();
        ClearPendingShot();

        EnemyController bestNext = null;
        int bestTargeterCount = int.MaxValue;
        float bestSqrDist = float.MaxValue;

        for (int i = 0; i < EnemyController.ActiveEnemies.Count; i++)
        {
            EnemyController candidate = EnemyController.ActiveEnemies[i];
            if (candidate == null || candidate == _enemy || !candidate.IsTargetableBySpirit)
                continue;

            float dx = candidate.transform.position.x - transform.position.x;
            float dz = candidate.transform.position.z - transform.position.z;
            float sqrDistFromSpirit = dx * dx + dz * dz;

            float candidateRange = GetEffectiveAttackRange(candidate);
            if (sqrDistFromSpirit > candidateRange * candidateRange)
                continue;

            int otherTargeters = CountOtherSpiritsTargeting(candidate);

            if (otherTargeters < bestTargeterCount ||
                (otherTargeters == bestTargeterCount && sqrDistFromSpirit < bestSqrDist))
            {
                bestTargeterCount = otherTargeters;
                bestSqrDist = sqrDistFromSpirit;
                bestNext = candidate;
            }
        }

        if (bestNext == null)
            return false;

        _enemy = bestNext;
        _lastKnownEnemyPos = bestNext.transform.position;
        _hasReachedShoreWaitSpot = false;
        _wasTargetHiddenUnderwater = false;
        _hasEngagedCurrentTarget = false;
        _offIslandOutOfRangeTimer = 0f;
        _nextPursuitRepath = 0f;
        IsChasingEnemy = false;
        StopNavigation();
        return true;
    }

    private int CountOtherSpiritsTargeting(EnemyController targetEnemy)
    {
        if (targetEnemy == null) return 0;
        int count = 0;
        for (int i = 0; i < ActiveSpirits.Count; i++)
        {
            SpiritController other = ActiveSpirits[i];
            if (other == null || other == this) continue;
            if (other.State == SpiritState.Fighting && other._enemy == targetEnemy)
                count++;
        }
        return count;
    }

    private void BeginAttackSwing()
    {
        TreeRegistry registry = TreeRegistry.Instance;
        ElementSettings elements = registry != null ? registry.Settings : null;
        ElementType spiritElement = Element;

        float damage = _combatSettings.Damage + (_buffs != null ? _buffs.GetTotal(BuffType.SpiritDamage) : 0f);
        float attackSpeed = _combatSettings.BaseAttackSpeedMultiplier;

        if (elements != null && _enemy != null && _enemy.Definition != null)
        {
            damage *= _enemy.Definition.GetDamageMultiplierFrom(spiritElement, elements);

            if (spiritElement != ElementType.None)
            {
                int power = registry.GetTotalPower(spiritElement);
                damage *= elements.GetDamageBonusMultiplier(power);
                attackSpeed *= registry.GetAttackSpeedMultiplier(spiritElement);
            }
        }

        attackSpeed *= 1f + (_buffs != null ? _buffs.GetTotal(BuffType.SpiritAttackSpeed) : 0f);
        attackSpeed *= _itemStatus.AttackSpeedMultiplier;
        TeamworkSettings teamwork = _combatSettings.TeamworkSettings;
        if (teamwork != null)
        {
            int count = Mathf.Max(1, _enemy.AttackerCount);
            damage *= teamwork.GetMultiplier(count) / count;
        }

        bool isSpecialAttack = false;
        if (_animator != null)
        {
            isSpecialAttack = _animator.PlayAttack(attackSpeed);
        }

        if (isSpecialAttack)
        {
            damage *= _specialAttackDamageMultiplier;
        }

        float itemDamageMultiplier = _itemStatus.DamageMultiplier;
        if (_logItemDamageSnapshots && Time.time >= _nextItemDamageLogTime)
        {
            _nextItemDamageLogTime = Time.time + 1f;
            Debug.Log($"Item damage snapshot: current={damage}, empowerment x{itemDamageMultiplier}, final={damage * itemDamageMultiplier}, special={isSpecialAttack}", this);
        }
        damage *= itemDamageMultiplier;
        _pendingEnemy = _enemy;
        _pendingDamage = damage;
        _pendingProjectileScale = isSpecialAttack ? _specialProjectileScale : 1f;
        _hasPendingShot = true;
        _isSpecialPendingShot = isSpecialAttack;

        _attackCooldown = _combatSettings.AttackInterval / Mathf.Max(0.01f, attackSpeed);
    }

    public void TriggerAnimationProjectileRelease()
    {
        if (Time.timeScale <= 0f) return;
        if (!_hasPendingShot) return;

        EnemyController target = _pendingEnemy != null ? _pendingEnemy : _enemy;
        float damage = _pendingDamage;
        float scale = _pendingProjectileScale;
        bool isSpecial = _isSpecialPendingShot;
        ClearPendingShot();

        if (target == null || !target.IsTargetableBySpirit) return;

        SpiritProjectile prefab = _view != null ? _view.ProjectilePrefab : null;
        if (prefab != null)
        {
            Transform spawnPoint = _view.ProjectileSpawnPoint;
            Vector3 direction = target.transform.position - spawnPoint.position;
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction.normalized)
                : transform.rotation;

            SpiritProjectile projectile = Instantiate(prefab, spawnPoint.position, rotation);
            projectile.Initialize(target, damage, scale);
            AudioManager.Instance.PlaySpiritAttack(Element, isSpecial, spawnPoint.position);
        }
        else
        {
            target.TakeDamage(damage, transform.position);
            AudioManager.Instance.PlaySpiritAttack(Element, isSpecial, transform.position);
        }
    }

    private void FaceDirection(Vector3 flatDirection)
    {
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(flatDirection.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _turnSpeed * Time.deltaTime);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
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
        enabled = gameObject.activeInHierarchy && _itemStatus.IsActive;
    }
}