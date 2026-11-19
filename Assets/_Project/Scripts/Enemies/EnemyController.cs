using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyState { Idle, Moving, Attacking }


public class EnemyController : MonoBehaviour
{
    public EnemyDefinition Definition { get; private set; }
    public EnemyState State { get; private set; } = EnemyState.Idle;
    public float Hp { get; private set; }
    public bool IsAlive => Hp > 0f;
    public int AttackerCount => _attackers.Count;
    public event Action<EnemyController> OnDied;

    private readonly List<SpiritController> _attackers = new List<SpiritController>();
    private TreeNode _target;
    private float _attackCooldown;

    private Vector3 _rangedStopPoint;
    private bool _hasRangedStopPoint;
    private bool _reachedRangedStopPoint;

    public void Initialize(EnemyDefinition definition)
    {
        Definition = definition;
        Hp = definition != null ? definition.MaxHp : 0f;
        _target = null;
        _attackCooldown = 0f;
        _hasRangedStopPoint = false;
        _reachedRangedStopPoint = false;
        _attackers.Clear();
        State = EnemyState.Idle;
    }

    public void Initialize(EnemyDefinition definition, Vector3 rangedStopPoint)
    {
        Initialize(definition);
        _rangedStopPoint = rangedStopPoint;
        _hasRangedStopPoint = true;
    }

    public void AddAttacker(SpiritController spirit)
    {
        if (spirit != null && IsAlive && !_attackers.Contains(spirit))
            _attackers.Add(spirit);
    }

    public void RemoveAttacker(SpiritController spirit)
    {
        _attackers.Remove(spirit);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f) return;

        Hp = Mathf.Max(0f, Hp - amount);
        if (IsAlive) return;

        State = EnemyState.Idle;
        enabled = false;
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }

    private void Update()
    {
        if (Definition == null || !IsAlive) return;

        if (Definition.ApproachType == EnemyApproachType.Ranged)
        {
            if (!_hasRangedStopPoint)
            {
                State = EnemyState.Idle;
                return;
            }
            if (!_reachedRangedStopPoint)
            {
                MoveToRangedStopPoint();
                return;
            }
        }

        // Re-target when the current tree died or was hidden (block re-corrupted).
        if (_target == null || !_target.IsTargetable)
        {
            _target = null;
            if (!TryAcquireTarget())
            {
                State = EnemyState.Idle;
                return;
            }
        }

        // Ground-plane distance, the enemy keeps its own Y.
        Vector3 toTarget = _target.TargetPoint - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance > Definition.AttackRange)
        {
            if (Definition.ApproachType == EnemyApproachType.Ranged)
            {
                // Search again next frame; ranged enemies never enter the island.
                _target = null;
                State = EnemyState.Idle;
                return;
            }

            State = EnemyState.Moving;
            Vector3 dir = toTarget / distance;
            transform.position = Vector3.MoveTowards(
                transform.position,
                transform.position + toTarget,
                Definition.MoveSpeed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(dir);
            return;
        }

        State = EnemyState.Attacking;
        _attackCooldown -= Time.deltaTime;
        if (_attackCooldown <= 0f)
        {
            _attackCooldown = Definition.AttackInterval;
            _target.Data.TakeDamage(Definition.Damage);
        }
    }

    private bool TryAcquireTarget()
    {
        TreeRegistry registry = TreeRegistry.Instance;
        if (registry == null) return false;
        return registry.TryGetNearest(transform.position, out _target);
    }

    private void MoveToRangedStopPoint()
    {
        Vector3 destination = _rangedStopPoint;
        destination.y = transform.position.y;
        Vector3 direction = destination - transform.position;
        float distance = direction.magnitude;
        float step = Mathf.Max(0f, Definition.MoveSpeed) * Time.deltaTime;
        if (distance <= step)
        {
            transform.position = destination;
            _reachedRangedStopPoint = true;
            State = EnemyState.Idle;
            return;
        }
        State = EnemyState.Moving;
        transform.position = Vector3.MoveTowards(transform.position, destination, step);
        transform.rotation = Quaternion.LookRotation(direction / distance);
    }

}
