using UnityEngine;

public enum EnemyApproachType
{
    Melee,
    Ranged
}

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Game/EnemyDefinition")]
public class EnemyDefinition : ScriptableObject
{
    // TODO(balance): all values below are placeholders.
    [SerializeField] private EnemyApproachType _approachType = EnemyApproachType.Melee;
    [SerializeField] private float _maxHp = 50f;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _attackInterval = 1f;
    [SerializeField] private float _attackRange = 1.5f;

    public EnemyApproachType ApproachType => _approachType;
    public float MaxHp => _maxHp;
    public float MoveSpeed => _moveSpeed;
    public float Damage => _damage;
    public float AttackInterval => _attackInterval;
    public float AttackRange => _attackRange;
}
