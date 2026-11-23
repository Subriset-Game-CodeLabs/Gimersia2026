using System.Collections.Generic;
using UnityEngine;

public class SpiritOcclusionController : MonoBehaviour
{
    private const int MaxSpirits = 3;

    [Header("References")]
    [SerializeField] private Camera _isometricCamera;
    [SerializeField] private SpiritSpawner _spiritSpawner;

    [Header("Capsule Cutout Shape")]
    [Tooltip("Layer objek 3D yang bisa menghalangi Spirit (misal: Trees, Props, Cliffs). Jangan centang Ground/Spirit.")]
    [SerializeField] private LayerMask _occluderLayers;
    [Tooltip("Radius kapsul tembus pandang di sekitar tubuh Spirit.")]
    [SerializeField, Min(0.1f)] private float _cutoutRadius = 0.65f;
    [Tooltip("Tinggi bagian atas kapsul dari kaki Spirit.")]
    [SerializeField, Min(0.5f)] private float _spiritTopOffset = 1.6f;
    [Tooltip("Tinggi bagian bawah kapsul dari kaki Spirit.")]
    [SerializeField, Min(0f)] private float _spiritBottomOffset = 0.2f;
    [SerializeField, Min(1f)] private float _fadeSpeed = 10f;

    private readonly Vector4[] _bottomPositions = new Vector4[MaxSpirits];
    private readonly Vector4[] _topPositions = new Vector4[MaxSpirits];
    private readonly float[] _currentRadii = new float[MaxSpirits];
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

    private static readonly int BottomsId = Shader.PropertyToID("_SpiritCutoutBottoms");
    private static readonly int TopsId = Shader.PropertyToID("_SpiritCutoutTops");
    private static readonly int RadiiId = Shader.PropertyToID("_SpiritCutoutRadii");

    private void Awake()
    {
        if (_isometricCamera == null)
            _isometricCamera = Camera.main;

        if (_spiritSpawner == null)
            _spiritSpawner = FindFirstObjectByType<SpiritSpawner>();
    }

    private void LateUpdate()
    {
        if (_isometricCamera == null || _spiritSpawner == null) return;

        IReadOnlyList<SpiritView> spirits = _spiritSpawner.Spirits;
        Vector3 camPos = _isometricCamera.transform.position;

        for (int i = 0; i < MaxSpirits; i++)
        {
            if (i >= spirits.Count || spirits[i] == null)
            {
                _currentRadii[i] = 0f;
                continue;
            }

            Vector3 spiritPos = spirits[i].transform.position;
            Vector3 bottom = spiritPos + Vector3.up * _spiritBottomOffset;
            Vector3 top = spiritPos + Vector3.up * _spiritTopOffset;
            Vector3 center = (bottom + top) * 0.5f;

            _bottomPositions[i] = new Vector4(bottom.x, bottom.y, bottom.z, 1f);
            _topPositions[i] = new Vector4(top.x, top.y, top.z, 1f);

            bool occluded = IsSpiritOccluded(camPos, center, spirits[i].transform);
            float targetRadius = occluded ? _cutoutRadius : 0f;
            _currentRadii[i] = Mathf.MoveTowards(_currentRadii[i], targetRadius, _cutoutRadius * _fadeSpeed * Time.deltaTime);
        }

        Shader.SetGlobalVectorArray(BottomsId, _bottomPositions);
        Shader.SetGlobalVectorArray(TopsId, _topPositions);
        Shader.SetGlobalFloatArray(RadiiId, _currentRadii);
    }

    private bool IsSpiritOccluded(Vector3 camPos, Vector3 spiritCenter, Transform spiritRoot)
    {
        Vector3 toSpirit = spiritCenter - camPos;
        float distance = toSpirit.magnitude;
        if (distance <= 0.1f) return false;

        Vector3 dir = toSpirit / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            camPos,
            _cutoutRadius * 0.6f,
            dir,
            _hitBuffer,
            Mathf.Max(0.05f, distance - 0.35f),
            _occluderLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i].collider;
            if (col != null && !col.transform.IsChildOf(spiritRoot))
                return true;
        }

        return false;
    }

    private void OnDisable()
    {
        for (int i = 0; i < MaxSpirits; i++)
            _currentRadii[i] = 0f;

        Shader.SetGlobalFloatArray(RadiiId, _currentRadii);
    }
}