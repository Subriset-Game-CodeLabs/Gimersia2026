using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Idle, Moving, Attacking }

public class EnemyController : MonoBehaviour
{
    public static readonly List<EnemyController> ActiveEnemies = new List<EnemyController>();

    private enum BehaviorPhase
    {
        None,
        // Skeleton & Monster (AmphibiousMelee)
        AmphibiousEmerging,
        AmphibiousSwimming,
        AmphibiousJumpingToIsland,
        AmphibiousOnIsland,

        // Worm (AquaticAmbushRanged: SpawnPoint -> DirectionPoint [random N kali] -> LastPoint)
        WormEmerging,
        WormSurfaceIdle,
        WormSubmerging,
        WormHiddenBetweenSpawns,
        WormAnchoredAttacking,

        // Bat (FlyingRanged)
        FlyingApproach,
        FlyingHoverAttacking,

        // Death
        Dying
    }

    private enum WormStage
    {
        SpawnPoint = 0,
        DirectionPoint = 1,
        LastPoint = 2
    }

    [Header("Ketinggian Peta (Dapat Diatur di Inspector)")]
    [SerializeField] private float _seaSurfaceY = -1.63f;
    [SerializeField] private float _islandSurfaceY = -1.0f;

    [Header("Hover Slowdown (Mempermudah Klik Saat Musuh Berjalan)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _hoveredSpeedMultiplier = 0.3f;

    [Header("Pengaturan Skeleton & Monster (Laut -> Lompat Pulau)")]
    [SerializeField] private float _seaSubmergeDepth = 1.35f;
    [SerializeField] private float _halfBodyAboveWaterOffset = 0.45f;
    [SerializeField] private float _jumpArcHeight = 0.95f;
    [SerializeField, Min(0.15f)] private float _jumpDuration = 0.75f;
    [SerializeField, Min(0.3f)] private float _shoreJumpTriggerDistance = 1.55f;

    [Header("Pengaturan Worm (SpawnPoint -> DirectionPoint -> LastPoint)")]
    [Tooltip("Jumlah kemunculan minimum Worm di DirectionPoint sebelum pindah ke LastPoint.")]
    [SerializeField, Min(0)] private int _wormMinDirectionEmergences = 2;
    [Tooltip("Jumlah kemunculan maksimum Worm di DirectionPoint sebelum pindah ke LastPoint.")]
    [SerializeField, Min(0)] private int _wormMaxDirectionEmergences = 5;
    [SerializeField] private float _wormEmergenceDuration = 0.7f;
    [SerializeField] private float _wormSurfacePauseDuration = 1.1f;
    [SerializeField] private float _wormSubmergeDuration = 0.65f;
    [SerializeField, Min(0.1f)] private float _wormHiddenRespawnDelay = 0.75f;

    [Header("Pengaturan Bat (Terbang)")]
    [SerializeField] private float _batFlightHeightAboveIsland = 1.65f;

    [Header("Navigation & Ranged")]
    [SerializeField, Min(0.05f)] private float _repathInterval = 0.5f;
    [SerializeField, Min(0.01f)] private float _destinationSampleRadius = 0.35f;
    [SerializeField] private Transform _firePoint;

    [Header("Animasi & Death Dissolve")]
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0.1f)] private float _loseAnimationDuration = 1.35f;

    public EnemyDefinition Definition { get; private set; }
    public EnemyState State { get; private set; } = EnemyState.Idle;
    public float Hp { get; private set; }
    public bool IsAlive => Hp > 0f && !_deathStarted;
    public bool IsHiddenUnderwater => _isHiddenUnderwater;
    public bool IsTargetableBySpirit => IsAlive && !_isHiddenUnderwater;
    public bool IsWorm => Definition != null && Definition.MovementArchetype == EnemyMovementArchetype.AquaticAmbushRanged;
    public bool IsWormAtLastPoint => IsWorm && _wormStage == WormStage.LastPoint;
    public int AttackerCount => _attackers.Count;
    public event Action<EnemyController> OnDied;

    public bool IsOffIslandOrFlying
    {
        get
        {
            if (Definition != null && Definition.MovementArchetype == EnemyMovementArchetype.FlyingRanged)
                return true;

            if (_phase == BehaviorPhase.AmphibiousOnIsland)
                return false;

            Vector3 probe = new Vector3(transform.position.x, _islandSurfaceY, transform.position.z);
            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                float dx = hit.position.x - transform.position.x;
                float dz = hit.position.z - transform.position.z;
                if ((dx * dx + dz * dz) <= 0.45f * 0.45f)
                    return false;
            }
            return true;
        }
    }

    private static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
    private static readonly int AnimAttackHash = Animator.StringToHash("Attack");
    private static readonly int AnimAttackIndexHash = Animator.StringToHash("AttackIndex");
    private static readonly int AnimJumpHash = Animator.StringToHash("Jump");
    private static readonly int AnimSpawnHash = Animator.StringToHash("Spawn");
    private static readonly int AnimSubmergeHash = Animator.StringToHash("Submerge");
    private static readonly int AnimLoseHash = Animator.StringToHash("Lose");

    private readonly List<SpiritController> _attackers = new List<SpiritController>();
    private readonly List<Transform> _spawnPoints = new List<Transform>();
    private readonly List<Transform> _directionPoints = new List<Transform>();
    private readonly List<Transform> _lastPoints = new List<Transform>();

    private NavMeshAgent _agent;
    private NavMeshPath _path;
    private Collider[] _colliders;
    private Renderer[] _allRenderers;
    private EnemyHPBar _hpBar;
    private EnemyVisualFeedback _visualFeedback;

    private TreeNode _target;
    private TreeUnderAttackIndicator _markedTreeIndicator;
    private BehaviorPhase _phase = BehaviorPhase.None;

    private float _attackCooldown;
    private float _nextRepath;
    private float _nextTargetSearch;
    private float _phaseTimer;
    private float _staggerTimer;
    private bool _hasMovePath;
    private bool _deathStarted;
    private bool _deathNotified;
    private bool _isHovered;
    private bool _isHiddenUnderwater;

    // Jump arc state
    private Vector3 _jumpStartPos;
    private Vector3 _jumpEndPos;

    // Worm 3-stage state
    private WormStage _wormStage = WormStage.SpawnPoint;
    private int _wormDirectionEmergencesRemaining;
    private Transform _currentWormPoint;

    private float CurrentSpeedMultiplier => _isHovered ? _hoveredSpeedMultiplier : 1f;

    private float CurrentIslandAgentSpeed
    {
        get
        {
            if (Definition == null) return 0.1f;
            return Mathf.Max(0.1f, Definition.MoveSpeed) * Definition.IslandMoveSpeedMultiplier * CurrentSpeedMultiplier;
        }
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _path = new NavMeshPath();
        _colliders = GetComponentsInChildren<Collider>(true);
        _allRenderers = GetComponentsInChildren<Renderer>(true);

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        _hpBar = GetComponent<EnemyHPBar>();
        if (_hpBar == null) _hpBar = gameObject.AddComponent<EnemyHPBar>();

        _visualFeedback = GetComponent<EnemyVisualFeedback>();
        if (_visualFeedback == null) _visualFeedback = gameObject.AddComponent<EnemyVisualFeedback>();

        if (_agent != null) _agent.enabled = false;
    }

    private void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
            ActiveEnemies.Add(this);
    }

    public void Initialize(
        EnemyDefinition definition,
        IReadOnlyList<Transform> spawnPoints = null,
        IReadOnlyList<Transform> directionPoints = null,
        IReadOnlyList<Transform> lastPoints = null)
    {
        if (this == null || _deathNotified) return;

        DisableAgent();
        Definition = definition;
        Hp = definition != null ? Mathf.Max(0f, definition.MaxHp) : 0f;
        SetTargetTree(null);
        _attackCooldown = 0f;
        _nextRepath = 0f;
        _nextTargetSearch = 0f;
        _staggerTimer = 0f;
        _deathStarted = false;
        _deathNotified = false;
        _isHovered = false;
        _isHiddenUnderwater = false;
        _currentWormPoint = null;
        _attackers.Clear();
        State = EnemyState.Idle;

        CopyPoints(spawnPoints, _spawnPoints);
        CopyPoints(directionPoints, _directionPoints);
        CopyPoints(lastPoints, _lastPoints);

        SetWormSurfaceVisible(true);
        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.ResetTrigger(AnimLoseHash);
            _animator.ResetTrigger(AnimJumpHash);
            _animator.ResetTrigger(AnimAttackHash);
            _animator.ResetTrigger(AnimSpawnHash);
            _animator.ResetTrigger(AnimSubmergeHash);
        }

        if (_hpBar != null) _hpBar.Bind(this, Hp);
        if (_visualFeedback != null) _visualFeedback.SetCombatHighlighted(false);

        // Worm belum menentukan pohon target sebelum tiba di LastPoint
        if (definition != null && definition.MovementArchetype != EnemyMovementArchetype.AquaticAmbushRanged)
        {
            TreeRegistry registry = TreeRegistry.Instance;
            if (registry != null && registry.TryGetNearest(transform.position, out TreeNode nearest))
            {
                SetTargetTree(nearest);
            }
        }

        SetupInitialSpawnPlacement();
    }

    private static void CopyPoints(IReadOnlyList<Transform> source, List<Transform> destination)
    {
        destination.Clear();
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                destination.Add(source[i]);
        }
    }

    private void SetupInitialSpawnPlacement()
    {
        if (Definition == null) return;

        Vector3 pos = transform.position;
        switch (Definition.MovementArchetype)
        {
            case EnemyMovementArchetype.AmphibiousMelee:
                pos.y = _seaSurfaceY - Mathf.Abs(_seaSubmergeDepth);
                transform.position = pos;
                _phase = BehaviorPhase.AmphibiousEmerging;
                SetAnimSpeed(1f);
                break;

            case EnemyMovementArchetype.AquaticAmbushRanged:
                _wormStage = WormStage.SpawnPoint;
                int minEmergences = Mathf.Max(0, _wormMinDirectionEmergences);
                int maxEmergences = Mathf.Max(minEmergences, _wormMaxDirectionEmergences);
                _wormDirectionEmergencesRemaining = _directionPoints.Count > 0
                    ? UnityEngine.Random.Range(minEmergences, maxEmergences + 1)
                    : 0;

                pos.y = _seaSurfaceY - Mathf.Abs(_seaSubmergeDepth);
                transform.position = pos;
                BeginWormEmergence();
                break;

            case EnemyMovementArchetype.FlyingRanged:
                pos.y = _islandSurfaceY + _batFlightHeightAboveIsland;
                transform.position = pos;
                _phase = BehaviorPhase.FlyingApproach;
                SetAnimSpeed(1f);
                break;
        }
    }

    public bool TryGetNearestIslandShorePoint(out Vector3 shoreEdgePoint)
    {
        shoreEdgePoint = Vector3.zero;
        Vector3 probe = new Vector3(transform.position.x, _islandSurfaceY, transform.position.z);

        if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 100f, NavMesh.AllAreas))
        {
            shoreEdgePoint = hit.position;
            return true;
        }

        return false;
    }

    public void SetHovered(bool hovered)
    {
        bool effectiveHover = hovered && IsTargetableBySpirit;
        if (_isHovered == effectiveHover) return;

        _isHovered = effectiveHover;
        if (_visualFeedback != null)
            _visualFeedback.SetHovered(_isHovered);

        if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh && Definition != null)
        {
            _agent.speed = CurrentIslandAgentSpeed;
        }
    }

    public void AddAttacker(SpiritController spirit)
    {
        if (this != null && spirit != null && IsTargetableBySpirit && isActiveAndEnabled && !_attackers.Contains(spirit))
        {
            _attackers.Add(spirit);
            UpdateCombatHighlight();
        }
    }

    public void RemoveAttacker(SpiritController spirit)
    {
        if (_attackers.Remove(spirit))
        {
            UpdateCombatHighlight();
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, transform.position - transform.forward);
    }

    public void TakeDamage(float amount, Vector3 hitSourcePosition)
    {
        if (this == null || !isActiveAndEnabled || !IsTargetableBySpirit || _deathStarted || !IsFinite(amount) || amount <= 0f)
            return;

        Hp = Mathf.Max(0f, Hp - amount);
        if (_hpBar != null) _hpBar.SetHp(Hp);

        if (_visualFeedback != null)
        {
            GameObject hitVfx = Definition != null ? Definition.HitVfxPrefab : null;
            _visualFeedback.PlayHitFeedback(hitSourcePosition, hitVfx);
        }

        if (IsAlive)
        {
            _staggerTimer = 0.08f;
            return;
        }

        BeginDeathSequence();
    }

    private void BeginDeathSequence()
    {
        _deathStarted = true;
        _isHovered = false;
        _isHiddenUnderwater = false;
        State = EnemyState.Idle;
        _phase = BehaviorPhase.Dying;
        _phaseTimer = 0f;

        ActiveEnemies.Remove(this);
        SetTargetTree(null);
        _attackers.Clear();
        UpdateCombatHighlight();
        SetCollidersEnabled(false);
        DisableAgent();

        if (_hpBar != null) _hpBar.Hide();

        SetAnimSpeed(0f);
        if (_animator != null)
        {
            _animator.ResetTrigger(AnimAttackHash);
            _animator.ResetTrigger(AnimJumpHash);
            _animator.SetTrigger(AnimLoseHash);
        }
    }

    private void CompleteDeathAndDestroy()
    {
        if (_deathNotified) return;
        _deathNotified = true;
        enabled = false;
        Destroy(gameObject);
        OnDied?.Invoke(this);
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
        SetTargetTree(null);
        _attackers.Clear();
        UpdateCombatHighlight();
        DisableAgent();
    }

    private void OnDestroy()
    {
        ActiveEnemies.Remove(this);
        SetTargetTree(null);
        _attackers.Clear();
        OnDied = null;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (Definition == null) return;

        if (_deathStarted)
        {
            UpdateDying();
            return;
        }

        if (!IsAlive) return;

        _attackCooldown = Mathf.Max(0f, _attackCooldown - Time.deltaTime);

        if (_staggerTimer > 0f)
        {
            _staggerTimer -= Time.deltaTime;
            return;
        }

        bool canSeekTree = Definition.MovementArchetype != EnemyMovementArchetype.AquaticAmbushRanged
            || _wormStage == WormStage.LastPoint;

        if (canSeekTree && (_target == null || !_target.IsEnemyTargetable))
        {
            SetTargetTree(null);
            if (Time.time >= _nextTargetSearch)
            {
                _nextTargetSearch = Time.time + Mathf.Max(0.1f, _repathInterval);
                TreeRegistry registry = TreeRegistry.Instance;
                if (registry != null && registry.TryGetNearest(transform.position, out TreeNode nearest))
                {
                    SetTargetTree(nearest);
                    _nextRepath = 0f;
                }
            }
        }

        switch (Definition.MovementArchetype)
        {
            case EnemyMovementArchetype.AmphibiousMelee:
                UpdateAmphibiousMelee();
                break;

            case EnemyMovementArchetype.AquaticAmbushRanged:
                UpdateWormAmbushRanged();
                break;

            case EnemyMovementArchetype.FlyingRanged:
                UpdateBatFlyingRanged();
                break;
        }

        UpdateCombatHighlight();
    }

    // =========================================================================
    // 1. SKELETON & MONSTER (Laut Cepat -> Lompat Pulau -> Darat Lebih Lambat)
    // =========================================================================
    private void UpdateAmphibiousMelee()
    {
        if (_phase == BehaviorPhase.AmphibiousJumpingToIsland)
        {
            UpdateIslandJump();
            return;
        }

        if (_target == null || !_target.IsEnemyTargetable)
        {
            StopNavigation();
            SetState(EnemyState.Idle);
            SetAnimSpeed(0f);
            return;
        }

        Vector3 targetPos = _target.TargetPoint;
        float seaSpeed = Mathf.Max(0.1f, Definition.MoveSpeed) * CurrentSpeedMultiplier;
        float emergedY = _seaSurfaceY - Mathf.Abs(_seaSubmergeDepth) + _halfBodyAboveWaterOffset;
        emergedY = Mathf.Min(emergedY, _seaSurfaceY + 0.15f);

        if (_phase == BehaviorPhase.AmphibiousEmerging)
        {
            SetState(EnemyState.Moving);
            SetAnimSpeed(1f);
            FaceTargetFlat(targetPos);

            if (TryFindIslandJumpLanding(targetPos, out Vector3 earlyLanding))
            {
                BeginIslandJump(earlyLanding);
                return;
            }

            Vector3 flatDir = FlatDirection(transform.position, targetPos);
            Vector3 nextPos = transform.position + flatDir * (seaSpeed * Time.deltaTime);
            nextPos.y = Mathf.MoveTowards(transform.position.y, emergedY, seaSpeed * 0.75f * Time.deltaTime);
            transform.position = nextPos;

            if (Mathf.Abs(transform.position.y - emergedY) <= 0.02f)
            {
                _phase = BehaviorPhase.AmphibiousSwimming;
            }
            return;
        }

        if (_phase == BehaviorPhase.AmphibiousSwimming)
        {
            SetState(EnemyState.Moving);
            SetAnimSpeed(1f);
            FaceTargetFlat(targetPos);

            if (TryFindIslandJumpLanding(targetPos, out Vector3 landingPoint))
            {
                BeginIslandJump(landingPoint);
                return;
            }

            Vector3 flatDir = FlatDirection(transform.position, targetPos);
            transform.position += flatDir * (seaSpeed * Time.deltaTime);
            return;
        }

        if (IsWithinGroundRange(transform.position, targetPos, Definition.AttackRange))
        {
            StopNavigation();
            FaceTargetFlat(targetPos);
            SetState(EnemyState.Attacking);
            SetAnimSpeed(0f);
            PerformMeleeOrRangedAttack();
            return;
        }

        PursueOnIslandNavMesh(targetPos);
    }

    private bool TryFindIslandJumpLanding(Vector3 targetPos, out Vector3 landingPoint)
    {
        landingPoint = Vector3.zero;
        Vector3 flatDir = FlatDirection(transform.position, targetPos);
        if (flatDir.sqrMagnitude < 0.001f) return false;

        float minIslandY = _seaSurfaceY + 0.18f;
        float searchHeight = Mathf.Max(_islandSurfaceY + 0.5f, _seaSurfaceY + 1.0f);

        const int steps = 4;
        for (int i = 1; i <= steps; i++)
        {
            float dist = (_shoreJumpTriggerDistance / steps) * i;
            Vector3 probe = transform.position + flatDir * dist;
            probe.y = searchHeight;

            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                if (hit.position.y < minIslandY) continue;

                float horizontalDist = Mathf.Sqrt(
                    (hit.position.x - transform.position.x) * (hit.position.x - transform.position.x) +
                    (hit.position.z - transform.position.z) * (hit.position.z - transform.position.z));

                if (horizontalDist <= _shoreJumpTriggerDistance + 0.35f)
                {
                    Vector3 inwardTarget = hit.position + flatDir * 0.45f;
                    if (NavMesh.SamplePosition(inwardTarget, out NavMeshHit inwardHit, 1.2f, NavMesh.AllAreas) &&
                        inwardHit.position.y >= minIslandY)
                    {
                        landingPoint = inwardHit.position;
                    }
                    else
                    {
                        landingPoint = hit.position;
                    }
                    return true;
                }
            }
        }

        return false;
    }

    private void BeginIslandJump(Vector3 landingPoint)
    {
        _phase = BehaviorPhase.AmphibiousJumpingToIsland;
        _phaseTimer = 0f;
        _jumpStartPos = transform.position;
        _jumpEndPos = landingPoint;
        SetState(EnemyState.Moving);
        SetAnimSpeed(0f);

        if (_animator != null)
        {
            _animator.ResetTrigger(AnimAttackHash);
            _animator.SetTrigger(AnimJumpHash);
        }
    }

    private void UpdateIslandJump()
    {
        _phaseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_phaseTimer / Mathf.Max(0.1f, _jumpDuration));

        Vector3 pos = Vector3.Lerp(_jumpStartPos, _jumpEndPos, t);
        pos.y += Mathf.Sin(t * Mathf.PI) * _jumpArcHeight;
        transform.position = pos;
        FaceTargetFlat(_jumpEndPos);

        if (t >= 1f)
        {
            transform.position = _jumpEndPos;
            AttachNavMeshOnIsland(_jumpEndPos);
            _phase = BehaviorPhase.AmphibiousOnIsland;
            SetAnimSpeed(1f);
        }
    }

    private void AttachNavMeshOnIsland(Vector3 position)
    {
        if (_agent == null) return;
        _agent.enabled = true;
        _agent.Warp(position);
        _agent.autoRepath = false;
        _agent.autoBraking = true;
        _agent.speed = CurrentIslandAgentSpeed;
        _agent.stoppingDistance = 0.05f;
        _agent.isStopped = true;
        _nextRepath = 0f;
    }

    private void PursueOnIslandNavMesh(Vector3 targetPos)
    {
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                AttachNavMeshOnIsland(hit.position);
            else
                return;
        }

        _agent.speed = CurrentIslandAgentSpeed;

        if (Time.time >= _nextRepath)
        {
            _nextRepath = Time.time + Mathf.Max(0.1f, _repathInterval);
            TrySetIslandAttackPath(targetPos);
        }

        bool moving = _hasMovePath && _agent.hasPath && _agent.remainingDistance > 0.08f;
        SetState(moving ? EnemyState.Moving : EnemyState.Idle);
        SetAnimSpeed(moving ? 1f : 0f);
    }

    private bool TrySetIslandAttackPath(Vector3 center)
    {
        float range = Mathf.Max(0.2f, Definition.AttackRange);
        Vector3 approach = transform.position - center;
        float startAngle = Mathf.Atan2(approach.z, approach.x);
        const int samples = 12;

        for (int ring = 0; ring < 2; ring++)
        {
            float radius = range * (ring == 0 ? 0.85f : 0.55f);
            for (int i = 0; i < samples; i++)
            {
                float angle = startAngle + i * (Mathf.PI * 2f / samples);
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _destinationSampleRadius, NavMesh.AllAreas))
                    continue;
                if (!_agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete)
                    continue;

                if (_agent.SetPath(_path))
                {
                    _agent.isStopped = false;
                    _hasMovePath = true;
                    return true;
                }
            }
        }

        _hasMovePath = false;
        return false;
    }

    // =========================================================================
    // 2. WORM (SpawnPoint -> DirectionPoint [Random N Kali] -> LastPoint [Tembak Pohon Terdekat])
    // =========================================================================
    private void BeginWormEmergence()
    {
        _phase = BehaviorPhase.WormEmerging;
        _phaseTimer = 0f;
        SetWormSurfaceVisible(true);
        SetState(EnemyState.Idle);
        SetAnimSpeed(0f);

        if (_wormStage == WormStage.LastPoint)
        {
            AcquireNearestTreeForWorm();
        }

        if (_target != null)
            FaceTargetFlat(_target.TargetPoint);
        else
            FaceTowardIslandShore();

        if (_animator != null) _animator.SetTrigger(AnimSpawnHash);
    }

    private void UpdateWormAmbushRanged()
    {
        float submergedY = _seaSurfaceY - Mathf.Abs(_seaSubmergeDepth);
        float surfacedY = _seaSurfaceY;

        switch (_phase)
        {
            case BehaviorPhase.WormEmerging:
                _phaseTimer += Time.deltaTime;
                float emergeT = Mathf.Clamp01(_phaseTimer / Mathf.Max(0.1f, _wormEmergenceDuration));
                Vector3 pEmerge = transform.position;
                pEmerge.y = Mathf.Lerp(submergedY, surfacedY, emergeT);
                transform.position = pEmerge;

                if (_target != null)
                    FaceTargetFlat(_target.TargetPoint);
                else
                    FaceTowardIslandShore();

                if (emergeT >= 1f)
                {
                    if (_wormStage == WormStage.LastPoint)
                    {
                        AcquireNearestTreeForWorm();
                        _phase = BehaviorPhase.WormAnchoredAttacking;
                    }
                    else
                    {
                        _phase = BehaviorPhase.WormSurfaceIdle;
                        _phaseTimer = 0f;
                    }
                }
                break;

            case BehaviorPhase.WormSurfaceIdle:
                SetState(EnemyState.Idle);
                SetAnimSpeed(0f);
                _phaseTimer += Time.deltaTime;
                if (_phaseTimer >= _wormSurfacePauseDuration)
                {
                    _phase = BehaviorPhase.WormSubmerging;
                    _phaseTimer = 0f;
                    if (_animator != null) _animator.SetTrigger(AnimSubmergeHash);
                }
                break;

            case BehaviorPhase.WormSubmerging:
                _phaseTimer += Time.deltaTime;
                float subT = Mathf.Clamp01(_phaseTimer / Mathf.Max(0.1f, _wormSubmergeDuration));
                Vector3 pSub = transform.position;
                pSub.y = Mathf.Lerp(surfacedY, submergedY, subT);
                transform.position = pSub;

                if (subT >= 1f)
                {
                    _phase = BehaviorPhase.WormHiddenBetweenSpawns;
                    _phaseTimer = 0f;
                    SetWormSurfaceVisible(false);
                    _attackers.Clear();
                }
                break;

            case BehaviorPhase.WormHiddenBetweenSpawns:
                _phaseTimer += Time.deltaTime;
                if (_phaseTimer >= _wormHiddenRespawnDelay)
                {
                    AdvanceWormToNextStage(submergedY);
                    BeginWormEmergence();
                }
                break;

            case BehaviorPhase.WormAnchoredAttacking:
                if (_target == null || !_target.IsEnemyTargetable)
                {
                    SetState(EnemyState.Idle);
                    SetAnimSpeed(0f);
                    return;
                }

                FaceTargetFlat(_target.TargetPoint);
                SetState(EnemyState.Attacking);
                SetAnimSpeed(0f);
                PerformMeleeOrRangedAttack();
                break;
        }
    }

    private void AdvanceWormToNextStage(float submergedY)
    {
        if (_wormDirectionEmergencesRemaining > 0 && _directionPoints.Count > 0)
        {
            _wormStage = WormStage.DirectionPoint;
            _wormDirectionEmergencesRemaining--;

            Transform dirPt = PickRandomPointExcluding(_directionPoints, _currentWormPoint);
            if (dirPt != null)
            {
                _currentWormPoint = dirPt;
                transform.position = new Vector3(dirPt.position.x, submergedY, dirPt.position.z);
            }
            return;
        }

        _wormStage = WormStage.LastPoint;
        if (_lastPoints.Count > 0)
        {
            Transform lastPt = PickRandomPointExcluding(_lastPoints, _currentWormPoint);
            if (lastPt != null)
            {
                _currentWormPoint = lastPt;
                transform.position = new Vector3(lastPt.position.x, submergedY, lastPt.position.z);
            }
        }
        else
        {
            transform.position = new Vector3(transform.position.x, submergedY, transform.position.z);
        }

        AcquireNearestTreeForWorm();
    }

    private void AcquireNearestTreeForWorm()
    {
        if (_target != null && _target.IsEnemyTargetable) return;

        TreeRegistry registry = TreeRegistry.Instance;
        if (registry != null && registry.TryGetNearest(transform.position, out TreeNode nearest))
        {
            SetTargetTree(nearest);
        }
    }

    private void FaceTowardIslandShore()
    {
        if (TryGetNearestIslandShorePoint(out Vector3 shoreEdge))
        {
            FaceTargetFlat(shoreEdge);
        }
    }

    private static Transform PickRandomPointExcluding(List<Transform> candidates, Transform excluded)
    {
        if (candidates == null || candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        int startIdx = UnityEngine.Random.Range(0, candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            Transform candidate = candidates[(startIdx + i) % candidates.Count];
            if (candidate != null && candidate != excluded)
                return candidate;
        }

        return candidates[startIdx];
    }

    private void SetWormSurfaceVisible(bool visible)
    {
        _isHiddenUnderwater = !visible;

        if (!visible)
        {
            _isHovered = false;
            if (_visualFeedback != null)
            {
                _visualFeedback.SetHovered(false);
                _visualFeedback.SetCombatHighlighted(false);
            }
        }

        SetCollidersEnabled(visible);

        if (_allRenderers != null)
        {
            for (int i = 0; i < _allRenderers.Length; i++)
            {
                if (_allRenderers[i] != null)
                    _allRenderers[i].enabled = visible;
            }
        }

        if (_hpBar != null)
        {
            if (visible && IsAlive)
                _hpBar.Show();
            else
                _hpBar.Hide();
        }
    }

    // =========================================================================
    // 3. BAT (Terbang dari Luar Peta -> Menembak Jarak Jauh)
    // =========================================================================
    private void UpdateBatFlyingRanged()
    {
        float flightY = _islandSurfaceY + _batFlightHeightAboveIsland;
        Vector3 currentPos = transform.position;
        currentPos.y = flightY;
        transform.position = currentPos;

        if (_target == null || !_target.IsEnemyTargetable)
        {
            SetState(EnemyState.Idle);
            SetAnimSpeed(0f);
            return;
        }

        Vector3 targetPos = _target.TargetPoint;
        FaceTargetFlat(targetPos);

        if (IsWithinGroundRange(transform.position, targetPos, Definition.AttackRange))
        {
            SetState(EnemyState.Attacking);
            SetAnimSpeed(0f);
            PerformMeleeOrRangedAttack();
            return;
        }

        SetState(EnemyState.Moving);
        SetAnimSpeed(1f);
        float effectiveSpeed = Mathf.Max(0.1f, Definition.MoveSpeed) * CurrentSpeedMultiplier;
        Vector3 flatDir = FlatDirection(transform.position, targetPos);
        transform.position += flatDir * (effectiveSpeed * Time.deltaTime);
    }

    // =========================================================================
    // SERANGAN (MELEE / RANGED & BASIC / SPECIAL)
    // =========================================================================
    private void PerformMeleeOrRangedAttack()
    {
        if (_attackCooldown > 0f || _target == null || !_target.IsEnemyTargetable) return;

        _attackCooldown = Mathf.Max(0.05f, Definition.AttackInterval);

        int attackIndex = 0;
        float finalDamage = Definition.Damage;

        if (Definition.AttackPattern == EnemyAttackPattern.BasicAndSpecial &&
            UnityEngine.Random.value < Definition.SpecialAttackChance)
        {
            attackIndex = 1;
            finalDamage *= Definition.SpecialAttackDamageMultiplier;
        }

        if (_animator != null)
        {
            _animator.SetInteger(AnimAttackIndexHash, attackIndex);
            _animator.SetTrigger(AnimAttackHash);
        }

        if (Definition.ApproachType == EnemyApproachType.Ranged)
        {
            if (Definition.ProjectilePrefab == null) return;
            Vector3 origin = _firePoint != null ? _firePoint.position : transform.position + Vector3.up * 0.9f;
            EnemyProjectile projectile = Instantiate(Definition.ProjectilePrefab, origin, transform.rotation);
            projectile.Launch(_target, finalDamage, Definition.ProjectileSpeed, Definition.ProjectileLifetime, this);
            return;
        }

        _target.Data.TakeDamage(finalDamage, this);
    }

    // =========================================================================
    // KEMATIAN (Animasi Lose -> Pose Frame Terakhir -> Dissolve)
    // =========================================================================
    private void UpdateDying()
    {
        _phaseTimer += Time.deltaTime;
        if (_phaseTimer < _loseAnimationDuration) return;

        if (_animator != null && _animator.speed > 0f)
        {
            _animator.speed = 0f;
        }

        if (_visualFeedback != null)
        {
            enabled = false;
            _visualFeedback.PlayDissolve(CompleteDeathAndDestroy);
        }
        else
        {
            CompleteDeathAndDestroy();
        }
    }

    // =========================================================================
    // HELPER & INDIKATOR
    // =========================================================================
    private void SetTargetTree(TreeNode nextTree)
    {
        if (_target == nextTree) return;

        if (_markedTreeIndicator != null)
        {
            _markedTreeIndicator.UnregisterAttacker(this);
            _markedTreeIndicator = null;
        }

        _target = nextTree;

        if (_target != null)
        {
            _markedTreeIndicator = _target.GetComponent<TreeUnderAttackIndicator>();
            if (_markedTreeIndicator == null)
                _markedTreeIndicator = _target.gameObject.AddComponent<TreeUnderAttackIndicator>();
            _markedTreeIndicator.RegisterAttacker(this);
        }
    }

    private void UpdateCombatHighlight()
    {
        if (_visualFeedback == null) return;
        bool inCombat = IsTargetableBySpirit && (_attackers.Count > 0 || (State == EnemyState.Attacking && _target != null));
        _visualFeedback.SetCombatHighlighted(inCombat);
    }

    private void SetState(EnemyState nextState)
    {
        if (State == nextState) return;
        State = nextState;
    }

    private void SetAnimSpeed(float speed)
    {
        if (_animator != null)
            _animator.SetFloat(AnimSpeedHash, speed);
    }

    private void FaceTargetFlat(Vector3 targetPos)
    {
        Vector3 dir = FlatDirection(transform.position, targetPos);
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
    }

    private static bool IsWithinGroundRange(Vector3 a, Vector3 b, float range)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return (dx * dx + dz * dz) <= range * range;
    }

    private void SetCollidersEnabled(bool value)
    {
        if (_colliders == null) return;
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = value;
        }
    }

    private void StopNavigation()
    {
        _hasMovePath = false;
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        if (_agent.hasPath || _agent.pathPending) _agent.ResetPath();
    }

    private void DisableAgent()
    {
        StopNavigation();
        if (_agent != null) _agent.enabled = false;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}