using UnityEngine;

public enum EnemyApproachType
{
    Melee,
    Ranged
}

public enum EnemyMovementArchetype
{
    AmphibiousMelee,     // Skeleton & Monster: muncul dari dalam laut -> naik -> lompat ke pulau
    AquaticAmbushRanged, // Worm: muncul vertikal dari laut di titik spawn -> menembak dari LastPoint
    FlyingRanged         // Bat: terbang dari luar map -> melayang dan menembak jarak jauh
}

public enum EnemyAttackPattern
{
    SingleBasic,     // 1 jenis animasi attack (Skeleton, Worm, Bat)
    BasicAndSpecial  // Basic attack + Special attack yang lebih jarang (Monster Air & Petir)
}

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Game/EnemyDefinition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Arketipe & Perilaku")]
    [SerializeField] private EnemyMovementArchetype _movementArchetype = EnemyMovementArchetype.AmphibiousMelee;
    [SerializeField] private EnemyApproachType _approachType = EnemyApproachType.Melee;
    [SerializeField] private EnemyAttackPattern _attackPattern = EnemyAttackPattern.SingleBasic;

    [Header("Elemen & Kelemahan")]
    [SerializeField] private ElementType _element = ElementType.None;
    [Tooltip("Centang khusus untuk Skeleton agar lemah terhadap Api, Air, maupun Petir sekaligus.")]
    [SerializeField] private bool _weakToAllElements;

    [Header("Stat Dasar")]
    [SerializeField] private float _maxHp = 50f;
    [SerializeField] private float _moveSpeed = 2f;
    [Tooltip("Pengali kecepatan gerak saat Skeleton/Monster sudah naik ke daratan pulau (contoh: 0.6 = melambat menjadi 60% dari kecepatan di laut).")]
    [Range(0.1f, 1.5f)]
    [SerializeField] private float _islandMoveSpeedMultiplier = 0.65f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _attackInterval = 1.4f;
    [SerializeField] private float _attackRange = 1.5f;

    [Header("Special Attack (Khusus Monster)")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float _specialAttackChance = 0.25f;
    [SerializeField, Min(1f)] private float _specialAttackDamageMultiplier = 1.5f;

    [Header("Ranged Projectile (Worm & Bat)")]
    [SerializeField] private EnemyProjectile _projectilePrefab;
    [SerializeField, Min(0.01f)] private float _projectileSpeed = 8f;
    [SerializeField, Min(0.01f)] private float _projectileLifetime = 5f;

    [Header("Hit VFX (Percikan Darah / Tulang)")]
    [SerializeField] private GameObject _hitVfxPrefab;

    public EnemyMovementArchetype MovementArchetype => _movementArchetype;
    public EnemyApproachType ApproachType => _approachType;
    public EnemyAttackPattern AttackPattern => _attackPattern;
    public ElementType Element => _element;
    public bool WeakToAllElements => _weakToAllElements;
    public float MaxHp => _maxHp;
    public float MoveSpeed => _moveSpeed;
    public float IslandMoveSpeedMultiplier => Mathf.Clamp(_islandMoveSpeedMultiplier, 0.1f, 1.5f);
    public float Damage => _damage;
    public float AttackInterval => _attackInterval;
    public float AttackRange => _attackRange;
    public float SpecialAttackChance => _specialAttackChance;
    public float SpecialAttackDamageMultiplier => _specialAttackDamageMultiplier;
    public EnemyProjectile ProjectilePrefab => _projectilePrefab;
    public float ProjectileSpeed => Mathf.Max(0.01f, _projectileSpeed);
    public float ProjectileLifetime => Mathf.Max(0.01f, _projectileLifetime);
    public GameObject HitVfxPrefab => _hitVfxPrefab;

    public float GetDamageMultiplierFrom(ElementType attackerElement, ElementSettings settings)
    {
        if (settings == null) return 1f;
        if (_weakToAllElements && attackerElement != ElementType.None)
            return settings.AdvantageDamageMultiplier;
        return settings.GetDamageMultiplier(attackerElement, _element);
    }
}