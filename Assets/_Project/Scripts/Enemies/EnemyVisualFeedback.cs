using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyVisualFeedback : MonoBehaviour
{
    [Header("Visual Root (Mesh / Armature Musuh)")]
    [Tooltip("Opsional: Parent model 3D musuh untuk efek recoil knockback. Otomatis terdeteksi jika dikosongkan.")]
    [SerializeField] private Transform _visualRoot;

    [Header("1. Hit Feedback: Knockback, White Flash & Blood/Bone VFX")]
    [SerializeField] private float _whiteFlashDuration = 0.13f;
    [SerializeField] private float _knockbackDistance = 0.20f;
    [SerializeField] private float _knockbackDuration = 0.16f;
    [SerializeField] private float _hitVfxHeight = 0.9f;

    [Header("2. Indikator Hover 3D (Lingkaran Bawah & Segitiga Atas)")]
    [Tooltip("Opsional: Prefab/Child lingkaran 3D di bawah musuh. Otomatis dibuat jika kosong.")]
    [SerializeField] private Transform _groundCircleIndicator;
    [Tooltip("Opsional: Prefab/Child segitiga 3D di atas kepala musuh. Otomatis dibuat jika kosong.")]
    [SerializeField] private Transform _hoverTriangleIndicator;
    [SerializeField] private float _triangleBaseHeight = 2.65f;
    [SerializeField] private float _triangleBobAmplitude = 0.14f;
    [SerializeField] private float _triangleBobSpeed = 5.5f;
    [SerializeField] private Color _hoverIndicatorColor = new Color(1f, 0.88f, 0.15f, 1f);

    [Header("3. Indikator Combat (Outline 3D & Lingkaran Bawah)")]
    [SerializeField] private Color _combatOutlineColor = new Color(1f, 0.22f, 0.15f, 1f);
    [SerializeField, Range(0.005f, 0.08f)] private float _outlineThickness = 0.025f;

    [Header("4. Efek Dissolve Setelah Animasi Lose")]
    [SerializeField] private float _dissolveDuration = 1.2f;
    [ColorUsage(true, true)]
    [SerializeField] private Color _dissolveGlowColor = new Color(1.4f, 0.45f, 0.95f, 1f);

    private Renderer[] _renderers;
    private Color[] _baseMainColors;
    private Color[] _baseRimColors;
    private Color[] _baseEmissionColors;
    private MaterialPropertyBlock _propBlock;

    private readonly List<GameObject> _outlineObjects = new List<GameObject>();
    private Material _outlineMaterial;
    private Renderer _circleRenderer;
    private Renderer _triangleRenderer;

    private Vector3 _visualBaseLocalPos;
    private Vector3 _knockbackDir;
    private float _flashTimer;
    private float _knockbackTimer;

    private bool _isHovered;
    private bool _isInCombat;
    private bool _isDissolving;
    private float _dissolveTimer;
    private Action _onDissolveComplete;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        CacheRenderers();
        EnsureIndicatorsBuilt();
        BuildSilhouetteOutlines();
        RefreshIndicators();
    }

    private void CacheRenderers()
    {
        if (_visualRoot == null)
        {
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.transform != transform)
                _visualRoot = anim.transform;
        }

        if (_visualRoot != null)
            _visualBaseLocalPos = _visualRoot.localPosition;

        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        var validList = new List<Renderer>();
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (_groundCircleIndicator != null && r.transform.IsChildOf(_groundCircleIndicator)) continue;
            if (_hoverTriangleIndicator != null && r.transform.IsChildOf(_hoverTriangleIndicator)) continue;
            validList.Add(r);
        }

        _renderers = validList.ToArray();
        _baseMainColors = new Color[_renderers.Length];
        _baseRimColors = new Color[_renderers.Length];
        _baseEmissionColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            Material mat = _renderers[i].sharedMaterial;
            if (mat == null) continue;
            _baseMainColors[i] = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId)
                : mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : Color.white;
            _baseRimColors[i] = mat.HasProperty(RimColorId) ? mat.GetColor(RimColorId) : Color.clear;
            _baseEmissionColors[i] = mat.HasProperty(EmissionColorId) ? mat.GetColor(EmissionColorId) : Color.black;
        }
    }

    public void PlayHitFeedback(Vector3 hitSourcePos, GameObject hitVfxPrefab)
    {
        if (_isDissolving) return;

        _flashTimer = _whiteFlashDuration;
        _knockbackTimer = _knockbackDuration;

        Vector3 away = transform.position - hitSourcePos;
        away.y = 0f;
        _knockbackDir = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

        if (hitVfxPrefab != null)
        {
            Vector3 vfxPos = transform.position + Vector3.up * _hitVfxHeight;
            Instantiate(hitVfxPrefab, vfxPos, Quaternion.identity);
        }
    }

    public void SetHovered(bool hovered)
    {
        if (_isHovered == hovered) return;
        _isHovered = hovered;
        RefreshIndicators();
    }

    public void SetCombatHighlighted(bool inCombat)
    {
        if (_isInCombat == inCombat) return;
        _isInCombat = inCombat;
        RefreshIndicators();
    }

    public void PlayDissolve(Action onComplete)
    {
        if (_isDissolving) return;

        _isDissolving = true;
        _dissolveTimer = 0f;
        _onDissolveComplete = onComplete;
        _isHovered = false;
        _isInCombat = false;
        RefreshIndicators();
    }

    private void RefreshIndicators()
    {
        bool showGroundCircle = !_isDissolving && (_isHovered || _isInCombat);
        bool showTriangle = !_isDissolving && _isHovered;
        bool showOutline = !_isDissolving && _isInCombat;

        if (_groundCircleIndicator != null)
        {
            _groundCircleIndicator.gameObject.SetActive(showGroundCircle);
            if (showGroundCircle && _circleRenderer != null)
            {
                Color c = _isHovered ? _hoverIndicatorColor : _combatOutlineColor;
                _circleRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(BaseColorId, c);
                _propBlock.SetColor(ColorId, c);
                _circleRenderer.SetPropertyBlock(_propBlock);
            }
        }

        if (_hoverTriangleIndicator != null)
        {
            _hoverTriangleIndicator.gameObject.SetActive(showTriangle);
        }

        for (int i = 0; i < _outlineObjects.Count; i++)
        {
            if (_outlineObjects[i] != null)
                _outlineObjects[i].SetActive(showOutline);
        }
    }

    private void Update()
    {
        // 1. Update visual knockback recoil
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer = Mathf.Max(0f, _knockbackTimer - Time.deltaTime);
            float t = 1f - (_knockbackTimer / Mathf.Max(0.001f, _knockbackDuration));
            float curve = Mathf.Sin(t * Mathf.PI) * _knockbackDistance;
            if (_visualRoot != null)
            {
                Vector3 localKnock = transform.InverseTransformDirection(_knockbackDir) * curve;
                _visualRoot.localPosition = _visualBaseLocalPos + localKnock;
            }
        }
        else if (_visualRoot != null && !_isDissolving && _visualRoot.localPosition != _visualBaseLocalPos)
        {
            _visualRoot.localPosition = _visualBaseLocalPos;
        }

        // 2. Update white hit flash
        if (_flashTimer > 0f && !_isDissolving)
        {
            _flashTimer = Mathf.Max(0f, _flashTimer - Time.deltaTime);
            float flashWeight = _flashTimer / Mathf.Max(0.001f, _whiteFlashDuration);
            ApplyWhiteFlash(flashWeight);
            if (_flashTimer <= 0f)
                ClearMeshProperties();
        }

        // 3. Animasi segitiga naik-turun saat di-hover
        if (_isHovered && _hoverTriangleIndicator != null && _hoverTriangleIndicator.gameObject.activeSelf)
        {
            float bobY = _triangleBaseHeight + Mathf.Sin(Time.time * _triangleBobSpeed) * _triangleBobAmplitude;
            _hoverTriangleIndicator.localPosition = new Vector3(0f, bobY, 0f);
            _hoverTriangleIndicator.Rotate(Vector3.up, 120f * Time.deltaTime, Space.Self);
        }

        // 4. Update Dissolve saat animasi Lose telah selesai
        if (_isDissolving)
        {
            _dissolveTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_dissolveTimer / Mathf.Max(0.05f, _dissolveDuration));
            ApplyDissolveProgress(progress);

            if (progress >= 1f)
            {
                _isDissolving = false;
                Action callback = _onDissolveComplete;
                _onDissolveComplete = null;
                callback?.Invoke();
            }
        }
    }

    private void ApplyWhiteFlash(float weight)
    {
        if (_renderers == null) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_propBlock);
            Color whiteColor = Color.Lerp(_baseMainColors[i], new Color(2.2f, 2.2f, 2.2f, 1f), weight);
            Color whiteRim = Color.Lerp(_baseRimColors[i], new Color(2f, 2f, 2f, 1f), weight);
            Color whiteEmit = Color.Lerp(_baseEmissionColors[i], new Color(1.5f, 1.5f, 1.5f, 1f), weight);

            _propBlock.SetColor(BaseColorId, whiteColor);
            _propBlock.SetColor(ColorId, whiteColor);
            _propBlock.SetColor(RimColorId, whiteRim);
            _propBlock.SetColor(EmissionColorId, whiteEmit);
            r.SetPropertyBlock(_propBlock);
        }
    }

    private void ApplyDissolveProgress(float progress)
    {
        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_propBlock);
                Color fadeMain = Color.Lerp(_baseMainColors[i], new Color(0.15f, 0.05f, 0.2f, 0f), progress);
                Color glow = Color.Lerp(_baseEmissionColors[i], _dissolveGlowColor, Mathf.Sin(progress * Mathf.PI));

                _propBlock.SetColor(BaseColorId, fadeMain);
                _propBlock.SetColor(ColorId, fadeMain);
                _propBlock.SetColor(RimColorId, glow);
                _propBlock.SetColor(EmissionColorId, glow);
                _propBlock.SetFloat(DissolveId, progress);
                _propBlock.SetFloat(CutoffId, progress);
                r.SetPropertyBlock(_propBlock);
            }
        }

        if (_visualRoot != null)
        {
            _visualRoot.localPosition = _visualBaseLocalPos + Vector3.down * (progress * 0.65f);
            float scale = Mathf.Lerp(1f, 0.85f, progress);
            _visualRoot.localScale = Vector3.one * scale;
        }
    }

    private void ClearMeshProperties()
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].SetPropertyBlock(null);
        }
    }

private void BuildSilhouetteOutlines()
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
        if (unlitShader == null) return;

        _outlineMaterial = new Material(unlitShader);
        _outlineMaterial.SetColor(BaseColorId, _combatOutlineColor);
        _outlineMaterial.SetColor(ColorId, _combatOutlineColor);
        if (_outlineMaterial.HasProperty("_Cull"))
            _outlineMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer src = _renderers[i];
            if (src == null) continue;

            // Normalisasi ketebalan berdasarkan lossyScale agar model dengan skala FBX besar (seperti Bat) tidak meledak
            Vector3 lossy = src.transform.lossyScale;
            float avgScale = Mathf.Max(0.001f, (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.y) + Mathf.Abs(lossy.z)) / 3f);
            float localThickness = _outlineThickness / avgScale;

            if (src is SkinnedMeshRenderer smr && smr.sharedMesh != null && smr.sharedMesh.isReadable)
            {
                GameObject outObj = new GameObject($"{src.name}_Outline");
                outObj.transform.SetParent(src.transform, false);
                SkinnedMeshRenderer outSmr = outObj.AddComponent<SkinnedMeshRenderer>();
                outSmr.sharedMesh = CreateExpandedMesh(smr.sharedMesh, localThickness);
                outSmr.bones = smr.bones;
                outSmr.rootBone = smr.rootBone;
                outSmr.sharedMaterial = _outlineMaterial;
                outSmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outSmr.receiveShadows = false;
                outObj.SetActive(false);
                _outlineObjects.Add(outObj);
            }
            else if (src is MeshRenderer mr)
            {
                MeshFilter mf = src.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;

                GameObject outObj = new GameObject($"{src.name}_Outline");
                outObj.transform.SetParent(src.transform, false);
                MeshFilter outMf = outObj.AddComponent<MeshFilter>();
                outMf.sharedMesh = CreateExpandedMesh(mf.sharedMesh, localThickness);

                MeshRenderer outMr = outObj.AddComponent<MeshRenderer>();
                outMr.sharedMaterial = _outlineMaterial;
                outMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outMr.receiveShadows = false;
                outObj.SetActive(false);
                _outlineObjects.Add(outObj);
            }
        }
    }

    private static Mesh CreateExpandedMesh(Mesh source, float amount)
    {
        Mesh clone = Instantiate(source);
        if (!source.isReadable) return clone;

        Vector3[] verts = clone.vertices;
        Vector3[] normals = clone.normals;
        if (normals != null && normals.Length == verts.Length)
        {
            for (int i = 0; i < verts.Length; i++)
                verts[i] += normals[i] * amount;
            clone.vertices = verts;
        }
        return clone;
    }

    private void EnsureIndicatorsBuilt()
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");

        if (_groundCircleIndicator == null)
        {
            GameObject circleObj = new GameObject("GroundCircleIndicator");
            circleObj.transform.SetParent(transform, false);
            circleObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            MeshFilter mf = circleObj.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateRingMesh(0.55f, 0.72f, 32);
            MeshRenderer mr = circleObj.AddComponent<MeshRenderer>();
            if (unlitShader != null)
            {
                Material mat = new Material(unlitShader);
                mat.SetColor(BaseColorId, _hoverIndicatorColor);
                mat.SetColor(ColorId, _hoverIndicatorColor);
                mr.sharedMaterial = mat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _groundCircleIndicator = circleObj.transform;
            _circleRenderer = mr;
        }
        else
        {
            _circleRenderer = _groundCircleIndicator.GetComponentInChildren<Renderer>(true);
        }

        if (_hoverTriangleIndicator == null)
        {
            GameObject triObj = new GameObject("HoverDownTriangleIndicator");
            triObj.transform.SetParent(transform, false);
            triObj.transform.localPosition = new Vector3(0f, _triangleBaseHeight, 0f);
            MeshFilter mf = triObj.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateDownwardTriangleMesh(0.24f, 0.34f);
            MeshRenderer mr = triObj.AddComponent<MeshRenderer>();
            if (unlitShader != null)
            {
                Material mat = new Material(unlitShader);
                mat.SetColor(BaseColorId, _hoverIndicatorColor);
                mat.SetColor(ColorId, _hoverIndicatorColor);
                mr.sharedMaterial = mat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _hoverTriangleIndicator = triObj.transform;
            _triangleRenderer = mr;
        }
        else
        {
            _triangleRenderer = _hoverTriangleIndicator.GetComponentInChildren<Renderer>(true);
        }
    }

    private static Mesh CreateRingMesh(float innerRadius, float outerRadius, int segments)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[segments * 2];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
            vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);

            int next = (i + 1) % segments;
            int triIdx = i * 6;
            triangles[triIdx] = i * 2;
            triangles[triIdx + 1] = next * 2 + 1;
            triangles[triIdx + 2] = i * 2 + 1;
            triangles[triIdx + 3] = i * 2;
            triangles[triIdx + 4] = next * 2;
            triangles[triIdx + 5] = next * 2 + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Mesh CreateDownwardTriangleMesh(float radius, float height)
    {
        Mesh mesh = new Mesh();
        Vector3 tip = new Vector3(0f, 0f, 0f);
        Vector3 top0 = new Vector3(0f, height, radius);
        Vector3 top1 = new Vector3(radius * 0.866f, height, -radius * 0.5f);
        Vector3 top2 = new Vector3(-radius * 0.866f, height, -radius * 0.5f);

        mesh.vertices = new[]
        {
            tip, top0, top1,
            tip, top1, top2,
            tip, top2, top0,
            top0, top2, top1
        };
        mesh.triangles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private void OnDestroy()
    {
        if (_outlineMaterial != null)
            Destroy(_outlineMaterial);
    }
}