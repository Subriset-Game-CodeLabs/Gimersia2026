using UnityEngine;

[RequireComponent(typeof(TreeNode))]
public class TreeElementTurret : MonoBehaviour
{
    private const int ProjectilesPerVolley = 3;

    [Header("Projectile Prefabs per Elemen (3D Sphere)")]
    [SerializeField] private SpiritProjectile _fireProjectilePrefab;
    [SerializeField] private SpiritProjectile _waterProjectilePrefab;
    [SerializeField] private SpiritProjectile _lightningProjectilePrefab;

    [Header("3 Titik Muncul Projectile per Elemen (Serangan Aktif Pohon Elemen)")]
    [SerializeField] private Transform[] _fireSpawnPoints = new Transform[ProjectilesPerVolley];
    [SerializeField] private Transform[] _waterSpawnPoints = new Transform[ProjectilesPerVolley];
    [SerializeField] private Transform[] _lightningSpawnPoints = new Transform[ProjectilesPerVolley];

    [SerializeField] private Vector3[] _fallbackSpawnOffsets =
    {
        new Vector3(-0.65f, 1.85f, 0f),
        new Vector3(0f, 2.25f, 0.25f),
        new Vector3(0.65f, 1.85f, 0f)
    };

    [Header("Stat Serangan Aktif Pohon Elemen (3 Projectile)")]
    [SerializeField, Min(1f)] private float _projectilePower = 10f;
    [SerializeField, Min(0.1f)] private float _fireInterval = 1.6f;
    [SerializeField, Min(1f)] private float _defenseRange = 7.5f;
    [SerializeField, Min(0.1f)] private float _projectileScale = 1f;
    [SerializeField, Min(0.5f)] private float _underAttackMemoryDuration = 3.5f;

    [Header("Tembakan Balasan 1x Saat Penurunan Fase")]
    [SerializeField] private bool _enableDowngradeShot = true;
    [Tooltip("Prefab 3D Sphere Projectile saat pohon turun dari Complete Netral -> Intermediate atau Intermediate -> Initial.")]
    [SerializeField] private SpiritProjectile _neutralDowngradeProjectilePrefab;
    [Tooltip("Titik muncul projectile saat turun fase (jika kosong, memakai posisi pohon + Downgrade Spawn Height).")]
    [SerializeField] private Transform _downgradeSpawnPoint;
    [SerializeField] private float _downgradeSpawnHeight = 1.6f;
    [SerializeField, Min(0.1f)] private float _downgradeProjectileScale = 1.25f;

    [Header("Power Projectile per Tingkat Penurunan Fase")]
    [Tooltip("Power projectile saat pohon turun dari Fase Elemen -> Complete.")]
    [SerializeField, Min(1f)] private float _powerElementToComplete = 35f;
    [Tooltip("Power projectile saat pohon turun dari Fase Complete -> Intermediate.")]
    [SerializeField, Min(1f)] private float _powerCompleteToIntermediate = 25f;
    [Tooltip("Power projectile saat pohon turun dari Fase Intermediate -> Initial.")]
    [SerializeField, Min(1f)] private float _powerIntermediateToInitial = 15f;

    [Header("Integrasi Rumus Elemen")]
    [SerializeField] private bool _useElementalMatrix = true;
    [SerializeField] private bool _useGlobalElementBuff = true;

    [Header("Optional VFX")]
    [SerializeField] private GameObject _shootVfxPrefab;
    [SerializeField] private GameObject _downgradeShootVfxPrefab;

    private TreeNode _treeNode;
    private EnemyController _currentAttacker;
    private float _lastDamagedTime = -999f;
    private float _fireCooldown;
    private bool _isCombatActive;
    private bool _subscribed;

    private readonly Collider[] _overlapBuffer = new Collider[24];

    private void Awake()
    {
        _treeNode = GetComponent<TreeNode>();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnDisable()
    {
        ClearCombatState();
    }

    private void TrySubscribe()
    {
        if (_subscribed || _treeNode == null || _treeNode.Data == null) return;

        _treeNode.Data.OnDamaged += HandleTreeDamaged;
        _treeNode.Data.OnDamagedByEnemy += HandleTreeDamagedByEnemy;
        _treeNode.Data.OnDowngradedByDamage += HandleDowngradedByDamage;
        _treeNode.Data.OnElementChanged += HandleElementOrStageChanged;
        _treeNode.Data.OnStageChanged += HandleElementOrStageChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _treeNode == null || _treeNode.Data == null) return;

        _treeNode.Data.OnDamaged -= HandleTreeDamaged;
        _treeNode.Data.OnDamagedByEnemy -= HandleTreeDamagedByEnemy;
        _treeNode.Data.OnDowngradedByDamage -= HandleDowngradedByDamage;
        _treeNode.Data.OnElementChanged -= HandleElementOrStageChanged;
        _treeNode.Data.OnStageChanged -= HandleElementOrStageChanged;
        _subscribed = false;
    }

    private void HandleElementOrStageChanged()
    {
        if (_treeNode == null || _treeNode.Data == null || !_treeNode.Data.HasElement)
        {
            ClearCombatState();
        }
    }

    private void HandleTreeDamagedByEnemy(EnemyController attacker, float amount)
    {
        if (attacker != null && attacker.IsAlive)
        {
            _currentAttacker = attacker;
        }

        if (_treeNode == null || _treeNode.Data == null || !_treeNode.Data.HasElement) return;
        if (_currentAttacker == null || !_currentAttacker.IsAlive) return;

        _lastDamagedTime = Time.time;
        _isCombatActive = true;
    }

    private void HandleTreeDamaged(float amount, Vector3 hitDirection)
    {
        if (_treeNode == null || _treeNode.Data == null || !_treeNode.Data.HasElement) return;

        _lastDamagedTime = Time.time;

        if (_currentAttacker == null || !_currentAttacker.IsAlive || !IsInDefenseRange(_currentAttacker))
        {
            _currentAttacker = FindAttackingEnemy(hitDirection);
        }

        if (_currentAttacker != null)
        {
            _isCombatActive = true;
        }
    }

    private void HandleDowngradedByDamage(
        TreeDowngradeStep step,
        ElementType lostElement,
        EnemyController attacker,
        Vector3 hitDirection)
    {
        if (!_enableDowngradeShot || _treeNode == null) return;

        EnemyController target = (attacker != null && attacker.IsAlive)
            ? attacker
            : (_currentAttacker != null && _currentAttacker.IsAlive && IsInDefenseRange(_currentAttacker))
                ? _currentAttacker
                : FindAttackingEnemy(hitDirection);

        if (target == null || !target.IsAlive) return;

        SpiritProjectile prefab = ResolveDowngradeProjectilePrefab(step, lostElement);
        if (prefab == null) return;

        float power = GetDowngradeStepPower(step);
        if (_useElementalMatrix && lostElement != ElementType.None && target.Definition != null)
        {
            TreeRegistry registry = TreeRegistry.Instance;
            if (registry != null && registry.Settings != null)
            {
                power *= registry.Settings.GetDamageMultiplier(lostElement, target.Definition.Element);
            }
        }

        Vector3 spawnPos = _downgradeSpawnPoint != null
            ? _downgradeSpawnPoint.position
            : transform.position + Vector3.up * _downgradeSpawnHeight;

        Vector3 aimDir = target.transform.position - spawnPos;
        aimDir.y = 0f;
        Quaternion rot = aimDir.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(aimDir.normalized)
            : Quaternion.identity;

        SpiritProjectile projectile = Instantiate(prefab, spawnPos, rot);
        projectile.Initialize(target, power, _downgradeProjectileScale);

        if (_downgradeShootVfxPrefab != null)
        {
            Instantiate(_downgradeShootVfxPrefab, spawnPos, rot);
        }
    }

    private SpiritProjectile ResolveDowngradeProjectilePrefab(TreeDowngradeStep step, ElementType lostElement)
    {
        if (step == TreeDowngradeStep.ElementToComplete && lostElement != ElementType.None)
        {
            SpiritProjectile elementalPrefab = GetProjectilePrefab(lostElement);
            if (elementalPrefab != null) return elementalPrefab;
        }

        if (_neutralDowngradeProjectilePrefab != null)
            return _neutralDowngradeProjectilePrefab;

        // Fallback jika prefab netral belum diisi di Inspector
        if (_fireProjectilePrefab != null) return _fireProjectilePrefab;
        if (_waterProjectilePrefab != null) return _waterProjectilePrefab;
        return _lightningProjectilePrefab;
    }

    private float GetDowngradeStepPower(TreeDowngradeStep step)
    {
        switch (step)
        {
            case TreeDowngradeStep.ElementToComplete:
                return _powerElementToComplete;
            case TreeDowngradeStep.CompleteToIntermediate:
                return _powerCompleteToIntermediate;
            case TreeDowngradeStep.IntermediateToInitial:
                return _powerIntermediateToInitial;
            default:
                return _powerIntermediateToInitial;
        }
    }

    private void Update()
    {
        if (!_isCombatActive) return;

        if (_treeNode == null || _treeNode.Data == null || !_treeNode.IsTargetable || !_treeNode.Data.HasElement)
        {
            ClearCombatState();
            return;
        }

        bool stillUnderAttack = (Time.time - _lastDamagedTime) <= _underAttackMemoryDuration;
        if (!stillUnderAttack)
        {
            ClearCombatState();
            return;
        }

        if (_currentAttacker == null || !_currentAttacker.IsAlive || !IsInDefenseRange(_currentAttacker))
        {
            _currentAttacker = FindAttackingEnemy(Vector3.zero);
            if (_currentAttacker == null)
            {
                ClearCombatState();
                return;
            }
        }

        _fireCooldown -= Time.deltaTime;
        if (_fireCooldown > 0f) return;

        FireVolleyAt(_currentAttacker);
    }

    private void FireVolleyAt(EnemyController target)
    {
        ElementType treeElement = _treeNode.Data.Element;
        SpiritProjectile prefab = GetProjectilePrefab(treeElement);
        if (prefab == null)
        {
            _fireCooldown = _fireInterval;
            return;
        }

        Transform[] spawnPoints = GetSpawnPointArray(treeElement);
        float damagePerProjectile = CalculateDamage(treeElement, target, out float attackSpeedMult);

        for (int i = 0; i < ProjectilesPerVolley; i++)
        {
            Vector3 spawnPos = ResolveSpawnPosition(spawnPoints, i);
            Vector3 aimDir = target.transform.position - spawnPos;
            aimDir.y = 0f;
            Quaternion rot = aimDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(aimDir.normalized)
                : Quaternion.identity;

            SpiritProjectile projectile = Instantiate(prefab, spawnPos, rot);
            projectile.Initialize(target, damagePerProjectile, _projectileScale);

            if (_shootVfxPrefab != null)
            {
                Instantiate(_shootVfxPrefab, spawnPos, rot);
            }
        }

        _fireCooldown = _fireInterval / Mathf.Max(0.1f, attackSpeedMult);
    }

    private float CalculateDamage(ElementType treeElement, EnemyController target, out float attackSpeedMultiplier)
    {
        float damage = _projectilePower;
        attackSpeedMultiplier = 1f;

        TreeRegistry registry = TreeRegistry.Instance;
        ElementSettings settings = registry != null ? registry.Settings : null;

        if (settings != null && target != null && target.Definition != null)
        {
            if (_useElementalMatrix)
            {
                damage *= settings.GetDamageMultiplier(treeElement, target.Definition.Element);
            }

            if (_useGlobalElementBuff && treeElement != ElementType.None)
            {
                int totalPower = registry.GetTotalPower(treeElement);
                damage *= settings.GetDamageBonusMultiplier(totalPower);
                attackSpeedMultiplier = registry.GetAttackSpeedMultiplier(treeElement);
            }
        }

        return damage;
    }

    private SpiritProjectile GetProjectilePrefab(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return _fireProjectilePrefab;
            case ElementType.Water: return _waterProjectilePrefab;
            case ElementType.Lightning: return _lightningProjectilePrefab;
            default: return null;
        }
    }

    private Transform[] GetSpawnPointArray(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return _fireSpawnPoints;
            case ElementType.Water: return _waterSpawnPoints;
            case ElementType.Lightning: return _lightningSpawnPoints;
            default: return null;
        }
    }

    private Vector3 ResolveSpawnPosition(Transform[] spawnPoints, int index)
    {
        if (spawnPoints != null && index < spawnPoints.Length && spawnPoints[index] != null)
        {
            return spawnPoints[index].position;
        }

        if (_fallbackSpawnOffsets != null && _fallbackSpawnOffsets.Length > 0)
        {
            Vector3 localOffset = _fallbackSpawnOffsets[index % _fallbackSpawnOffsets.Length];
            return transform.TransformPoint(localOffset);
        }

        return transform.position + Vector3.up * 2f;
    }

    private bool IsInDefenseRange(EnemyController enemy)
    {
        if (enemy == null) return false;
        Vector3 delta = enemy.transform.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= _defenseRange * _defenseRange;
    }

    private EnemyController FindAttackingEnemy(Vector3 hitDirection)
    {
        Vector3 expectedAttackerDir = new Vector3(-hitDirection.x, 0f, -hitDirection.z);
        bool hasDirectionHint = expectedAttackerDir.sqrMagnitude > 0.01f;
        if (hasDirectionHint) expectedAttackerDir.Normalize();

        EnemyController bestMatch = null;
        float bestScore = float.MinValue;

        int count = Physics.OverlapSphereNonAlloc(transform.position, _defenseRange, _overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null) continue;

            EnemyController enemy = col.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.IsAlive) continue;

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            float dist = toEnemy.magnitude;
            if (dist > _defenseRange) continue;

            float score = -dist;
            if (hasDirectionHint && dist > 0.001f)
            {
                float alignment = Vector3.Dot(toEnemy / dist, expectedAttackerDir);
                score += alignment * 5f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = enemy;
            }
        }

        return bestMatch;
    }

    private void ClearCombatState()
    {
        _isCombatActive = false;
        _currentAttacker = null;
        _fireCooldown = 0f;
    }
}