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
    [Tooltip("Aktifkan animasi bobbing & rotasi otomatis jika objek segitiga belum memiliki Animator sendiri.")]
    [SerializeField] private bool _useBuiltInTriangleMotion = true;
    [SerializeField] private float _triangleBobAmplitude = 0.12f;
    [SerializeField] private float _triangleBobFrequency = 3f;
    [SerializeField] private float _triangleRotateSpeed = 90f;

    [Header("References")]
    [SerializeField] private SpiritAnimator _spiritAnimator;

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

        CacheTriangleTransform();
    }

    private void CacheTriangleTransform()
    {
        if (_selectionTriangleIndicator != null && !_hasCachedTrianglePos)
        {
            _triangleBaseLocalPos = _selectionTriangleIndicator.transform.localPosition;
            _hasCachedTrianglePos = true;
        }
    }

    public void Initialize(int index)
    {
        Index = index;
        CacheTriangleTransform();
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (_selectionRing != null)
            _selectionRing.SetActive(selected);

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
        if (!IsSelected || !_useBuiltInTriangleMotion || _selectionTriangleIndicator == null) return;

        Transform t = _selectionTriangleIndicator.transform;
        float offsetY = Mathf.Sin(Time.time * _triangleBobFrequency) * _triangleBobAmplitude;
        t.localPosition = _triangleBaseLocalPos + new Vector3(0f, offsetY, 0f);
        t.Rotate(Vector3.up, _triangleRotateSpeed * Time.deltaTime, Space.Self);
    }
}