using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    // TODO(balance): small placeholder arrival radius, independent of prefab colliders.
    [SerializeField, Min(0f)] private float _hitTolerance = 0.1f;
    [SerializeField] private float _targetHeightOffset = 0.8f;

    private TreeNode _target;
    private EnemyController _owner;
    private PhaseManager _phases;
    private float _damage;
    private float _speed;
    private float _remainingLifetime;
    private bool _launched;
    private bool _retired;

    private void OnEnable()
    {
        BindPhases();
    }

    private void Start()
    {
        // Covers a phase manager whose Awake ran after this projectile's OnEnable.
        BindPhases();
    }

    public void Launch(TreeNode target, float damage, float speed, float lifetime, EnemyController owner = null)
    {
        if (_retired || _launched) return;
        if (target == null || !target.IsTargetable ||
            float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f || !IsPositiveFinite(speed) ||
            !IsPositiveFinite(lifetime) || !isActiveAndEnabled)
        {
            Retire();
            return;
        }

        _target = target;
        _owner = owner;
        _damage = damage;
        _speed = speed;
        _remainingLifetime = lifetime;
        _launched = true;
        _target.OnAvailabilityChanged += HandleTargetAvailabilityChanged;
        BindPhases();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (!_launched || _retired) return;
        if (_target == null || !_target.IsTargetable)
        {
            Retire();
            return;
        }

        _remainingLifetime -= Time.deltaTime;
        if (_remainingLifetime <= 0f)
        {
            Retire();
            return;
        }

        Vector3 targetPoint = _target.TargetPoint + Vector3.up * _targetHeightOffset;
        Vector3 position = transform.position;
        float distance = Vector3.Distance(position, targetPoint);
        float step = _speed * Time.deltaTime;
        float tolerance = Mathf.Max(0f, _hitTolerance);
        transform.position = Vector3.MoveTowards(position, targetPoint, step);

        // The swept travel distance reaches the target even on a large frame step.
        if (distance > step + tolerance) return;

        TreeNode hitTarget = _target;
        EnemyController attacker = _owner;
        float damage = _damage;
        Vector3 impactPos = transform.position;
        // Retire before damage: death/phase callbacks can run synchronously.
        Retire();
        if (hitTarget != null && hitTarget.IsTargetable)
        {
            AudioManager.Instance.PlayEnemyProjectileHit(impactPos);
            hitTarget.Data.TakeDamage(damage, attacker);
        }
    }

    private void BindPhases()
    {
        if (_retired) return;
        PhaseManager phases = PhaseManager.Instance;
        if (_phases != phases)
        {
            UnbindPhases();
            _phases = phases;
            if (_phases != null)
            {
                _phases.OnDayStart += HandleDayStart;
                _phases.OnRewardStart += HandleDayStart;
                _phases.OnRunFinished += Retire;
            }
        }

        // No manager is allowed for isolated tests; target/lifetime rules still apply.
        if (_launched && _phases != null && _phases.CurrentPhase != PhaseType.Night)
            Retire();
    }

    private void HandleDayStart(int day)
    {
        Retire();
    }

    private void HandleTargetAvailabilityChanged(TreeNode target)
    {
        if (!target.IsTargetable) Retire();
    }

    private void Retire()
    {
        if (_retired) return;
        _retired = true;
        _launched = false;
        UnbindTarget();
        UnbindPhases();
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        // Disabling an in-flight projectile cancels it, rather than resuming next day.
        if (_launched) Retire();
        UnbindTarget();
        UnbindPhases();
    }

    private void UnbindTarget()
    {
        if (_target != null)
            _target.OnAvailabilityChanged -= HandleTargetAvailabilityChanged;
        _target = null;
        _owner = null;
    }

    private void UnbindPhases()
    {
        if (_phases != null)
        {
            _phases.OnDayStart -= HandleDayStart;
            _phases.OnRewardStart -= HandleDayStart;
            _phases.OnRunFinished -= Retire;
        }
        _phases = null;
    }

    private static bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
    }
}