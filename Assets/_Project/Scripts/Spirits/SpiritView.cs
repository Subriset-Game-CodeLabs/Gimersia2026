using System;
using UnityEngine;

public class SpiritView : MonoBehaviour
{
    [Header("Element & Combat Visuals")]
    [SerializeField] private ElementType _element = ElementType.None;
    [SerializeField] private SpiritProjectile _projectilePrefab;
    [SerializeField] private Transform _projectileSpawnPoint;

    [Header("Selection Indicators")]
    [SerializeField] private GameObject _selectionRing;
    [SerializeField] private GameObject _selectionTriangleIndicator;
    [Tooltip("Objek 3D Cylinder di bawah Spirit yang muncul hanya saat Spirit terpilih DAN kursor meng-hover musuh.")]
    [SerializeField] private GameObject _attackRangeCylinder;
    [Tooltip("Aktifkan animasi bobbing & rotasi otomatis jika objek segitiga belum memiliki Animator sendiri.")]
    [SerializeField] private bool _useBuiltInTriangleMotion = true;
    [SerializeField] private float _triangleBobAmplitude = 0.12f;
    [SerializeField] private float _triangleBobFrequency = 3f;
    [SerializeField] private float _triangleRotateSpeed = 90f;

    [Header("References")]
    [SerializeField] private SpiritAnimator _spiritAnimator;
    [SerializeField] private SpiritController _spiritController;

    private Vector3 _triangleBaseLocalPos;
    private bool _hasCachedTrianglePos;

    public int Index { get; private set; }
    public ElementType Element => _element;
    public SpiritProjectile ProjectilePrefab => _projectilePrefab;
    public Transform ProjectileSpawnPoint => _projectileSpawnPoint != null ? _projectileSpawnPoint : transform;
    public bool IsSelected { get; private set; }

    public event Action<bool> OnSelectionChanged;

    private void Awake()
    {
        if (_spiritAnimator == null)
            _spiritAnimator = GetComponent<SpiritAnimator>();

        if (_spiritController == null)
            _spiritController = GetComponent<SpiritController>();

        CacheTriangleTransform();
        PrepareAttackRangeCylinder();
    }

    private void OnEnable()
    {
        EnemyHoverSelector.OnHoveredEnemyChanged += HandleHoveredEnemyChanged;
        UpdateAttackRangeCylinderVisibility();
    }

    private void OnDisable()
    {
        EnemyHoverSelector.OnHoveredEnemyChanged -= HandleHoveredEnemyChanged;
        if (_attackRangeCylinder != null)
            _attackRangeCylinder.SetActive(false);
    }

    private void HandleHoveredEnemyChanged(EnemyController hoveredEnemy)
    {
        UpdateAttackRangeCylinderVisibility();
    }

    private void CacheTriangleTransform()
    {
        if (_selectionTriangleIndicator != null && !_hasCachedTrianglePos)
        {
            _triangleBaseLocalPos = _selectionTriangleIndicator.transform.localPosition;
            _hasCachedTrianglePos = true;
        }
    }

    private void PrepareAttackRangeCylinder()
    {
        if (_attackRangeCylinder == null) return;

        Collider[] colliders = _attackRangeCylinder.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        SyncAttackRangeCylinderScale();
        _attackRangeCylinder.SetActive(false);
    }

    private void SyncAttackRangeCylinderScale()
    {
        if (_attackRangeCylinder == null) return;

        float attackRange = 0f;
        if (_spiritController != null && _spiritController.CombatSettings != null)
        {
            attackRange = _spiritController.CombatSettings.AttackRange;
        }

        float diameter = Mathf.Max(0.1f, attackRange * 2f);
        float parentScaleX = Mathf.Abs(transform.lossyScale.x) > 0.0001f ? Mathf.Abs(transform.lossyScale.x) : 1f;
        float parentScaleZ = Mathf.Abs(transform.lossyScale.z) > 0.0001f ? Mathf.Abs(transform.lossyScale.z) : 1f;

        Vector3 scale = _attackRangeCylinder.transform.localScale;
        scale.x = diameter / parentScaleX;
        scale.z = diameter / parentScaleZ;
        _attackRangeCylinder.transform.localScale = scale;
    }

    private void UpdateAttackRangeCylinderVisibility()
    {
        if (_attackRangeCylinder == null) return;

        EnemyController hovered = EnemyHoverSelector.CurrentHoveredEnemy;
        bool shouldShow = IsSelected && hovered != null && hovered.IsTargetableBySpirit;

        if (shouldShow)
            SyncAttackRangeCylinderScale();

        if (_attackRangeCylinder.activeSelf != shouldShow)
            _attackRangeCylinder.SetActive(shouldShow);
    }

    public void Initialize(int index)
    {
        Index = index;
        CacheTriangleTransform();
        PrepareAttackRangeCylinder();
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (_selectionRing != null)
            _selectionRing.SetActive(selected);

        UpdateAttackRangeCylinderVisibility();

        if (_selectionTriangleIndicator != null)
        {
            _selectionTriangleIndicator.SetActive(selected);
            if (selected && _hasCachedTrianglePos)
                _selectionTriangleIndicator.transform.localPosition = _triangleBaseLocalPos;
        }

        if (selected && _spiritAnimator != null)
            _spiritAnimator.NotifyPlayerInteraction();

        OnSelectionChanged?.Invoke(selected);
    }

    private void Update()
    {
        if (!IsSelected)
        {
            if (_attackRangeCylinder != null && _attackRangeCylinder.activeSelf)
                _attackRangeCylinder.SetActive(false);
            return;
        }

        UpdateAttackRangeCylinderVisibility();

        if (!_useBuiltInTriangleMotion || _selectionTriangleIndicator == null) return;

        Transform t = _selectionTriangleIndicator.transform;
        float offsetY = Mathf.Sin(Time.time * _triangleBobFrequency) * _triangleBobAmplitude;
        t.localPosition = _triangleBaseLocalPos + new Vector3(0f, offsetY, 0f);
        t.Rotate(Vector3.up, _triangleRotateSpeed * Time.deltaTime, Space.Self);
    }
}