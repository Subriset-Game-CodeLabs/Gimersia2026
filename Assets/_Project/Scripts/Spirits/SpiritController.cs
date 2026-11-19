using System;
using UnityEngine;

public enum SpiritState { Idle, Moving, Interacting, Fighting }


public class SpiritController : MonoBehaviour
{
    // TODO(balance): placeholder move speed.
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _arriveDistance = 0.05f;
    [SerializeField] private SpiritCombatSettings _combatSettings;

    private Vector3 _target;
    private IInteractable _interactable;
    private EnemyController _enemy;
    private Bounds _islandBounds;
    private float _attackCooldown;
    private bool _registeredAsAttacker;

    public SpiritState State { get; private set; } = SpiritState.Idle;

    public event Action OnArrived;
    // For the animation task: one place to react to idle, moving and working.
    public event Action<SpiritState> OnStateChanged;

    private void Awake()
    {
        // Idle spirits do no per-frame work.
        enabled = false;
    }

    private void OnDestroy()
    {
        // Never leave a ghost worker on a target.
        SetTarget(null);
        ClearEnemyTarget();
    }

    private void OnDisable()
    {
        if (State != SpiritState.Fighting) return;
        ClearEnemyTarget();
        SetState(SpiritState.Idle);
    }

    public void MoveTo(Vector3 worldPoint)
    {
        ClearEnemyTarget();
        SetTarget(null);
        BeginMove(worldPoint);
    }

    public void Interact(IInteractable target)
    {
        if (target == null || !target.CanInteract) return;

        ClearEnemyTarget();
        SetTarget(target);
        BeginMove(target.InteractionPoint);
    }

    public void Attack(EnemyController enemy, Bounds islandBounds)
    {
        if (enemy == null || !enemy.IsAlive || _combatSettings == null) return;

        ClearEnemyTarget();
        SetTarget(null);
        _enemy = enemy;
        _islandBounds = islandBounds;
        _attackCooldown = 0f;
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
        if (_interactable != null) _interactable.RemoveWorker(this);
        _interactable = target;
    }

    private void SetState(SpiritState state)
    {
        if (State == state) return;
        State = state;
        OnStateChanged?.Invoke(state);
    }

    private void BeginMove(Vector3 worldPoint)
    {
        // Stay on the spirit's own height so it slides along the ground.
        _target = new Vector3(worldPoint.x, transform.position.y, worldPoint.z);
        SetState(SpiritState.Moving);
        enabled = true;
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
        transform.position = Vector3.MoveTowards(transform.position, _target, _moveSpeed * Time.deltaTime);

        if ((transform.position - _target).sqrMagnitude > _arriveDistance * _arriveDistance) return;

        OnArrived?.Invoke();

        if (_interactable != null && _interactable.CanInteract)
        {
            // Register only on arrival, so walking spirits don't count for the multiplier.
            _interactable.AddWorker(this);
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
        if (!_interactable.CanInteract)
        {
            Stop();
            return;
        }

        _interactable.Interact(this, Time.deltaTime);
    }

    private void UpdateFighting()
    {
        if (_enemy == null || !_enemy.IsAlive || _combatSettings == null)
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
            // Bounds limit the order, but are not a navigable-island polygon.
            Vector3 destination = _enemy.transform.position;
            if (distance > 0f) destination -= toEnemy / distance * range;
            destination.x = Mathf.Clamp(destination.x, _islandBounds.min.x, _islandBounds.max.x);
            destination.z = Mathf.Clamp(destination.z, _islandBounds.min.z, _islandBounds.max.z);
            destination.y = transform.position.y;
            transform.position = Vector3.MoveTowards(
                transform.position, destination, _moveSpeed * Time.deltaTime);
            return;
        }

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
        SetTarget(null);
        ClearEnemyTarget();
        SetState(SpiritState.Idle);
        enabled = false;
    }
}
