using UnityEngine;

public class SpiritProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 14f;
    [SerializeField] private float _hitRadius = 0.35f;
    [SerializeField] private float _targetHeightOffset = 0.8f;
    [SerializeField] private float _maxLifetime = 5f;
    [SerializeField] private GameObject _hitVfxPrefab;

    private EnemyController _target;
    private Vector3 _lastKnownTargetPos;
    private float _damage;
    private float _lifetimeTimer;
    private bool _initialized;

    public void Initialize(EnemyController target, float damage, float scaleMultiplier = 1f)
    {
        _target = target;
        _damage = damage;
        _lifetimeTimer = _maxLifetime;
        _initialized = true;

        transform.localScale *= Mathf.Max(0.1f, scaleMultiplier);

        if (_target != null)
            _lastKnownTargetPos = _target.transform.position + Vector3.up * _targetHeightOffset;
        else
            _lastKnownTargetPos = transform.position + transform.forward * 5f;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (!_initialized) return;

        _lifetimeTimer -= Time.deltaTime;
        if (_lifetimeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (_target != null && _target.IsAlive)
        {
            _lastKnownTargetPos = _target.transform.position + Vector3.up * _targetHeightOffset;
        }

        Vector3 toDestination = _lastKnownTargetPos - transform.position;
        float distance = toDestination.magnitude;
        float step = _speed * Time.deltaTime;

        if (distance <= Mathf.Max(_hitRadius, step))
        {
            Impact();
            return;
        }

        Vector3 dir = toDestination / distance;
        transform.position += dir * step;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    private void Impact()
    {
        if (_target != null && _target.IsAlive)
        {
            _target.TakeDamage(_damage, transform.position);
        }

        if (_hitVfxPrefab != null)
        {
            Instantiate(_hitVfxPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}