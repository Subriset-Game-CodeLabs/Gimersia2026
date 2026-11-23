using System.Collections.Generic;
using UnityEngine;

public class TreeVisualFeedback : MonoBehaviour
{
    [Header("1. Click / Touchable Bounce")]
    [SerializeField] private float _clickDuration = 0.24f;
    [SerializeField] private float _clickSquashY = 0.84f;
    [SerializeField] private float _clickStretchXZ = 1.10f;

    [Header("2. Stage Transition Flash & Shake")]
    [SerializeField] private float _transitionDuration = 0.38f;
    [SerializeField] private float _stageUpShakeMagnitude = 0.06f;
    [SerializeField] private float _stageDownShakeMagnitude = 0.11f;
    [ColorUsage(true, true)]
    [SerializeField] private Color _stageUpFlashColor = new Color(1.8f, 1.55f, 0.75f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _stageDownFlashColor = new Color(1.7f, 0.25f, 0.25f, 1f);

    [Header("3. Enemy Hit Recoil & Flash")]
    [SerializeField] private float _hitRecoilDuration = 0.26f;
    [SerializeField] private float _hitTiltAngle = 11f;
    [ColorUsage(true, true)]
    [SerializeField] private Color _hitFlashColor = new Color(1.6f, 0.45f, 0.45f, 1f);

    private Transform _activeVisual;
    private Vector3 _baseLocalPos;
    private Vector3 _baseLocalScale = Vector3.one;
    private Quaternion _baseLocalRot = Quaternion.identity;

    private struct RendererEntry
    {
        public Renderer renderer;
        public Color baseColor;
        public Color baseRimColor;
    }

    private readonly List<RendererEntry> _renderers = new List<RendererEntry>();
    private MaterialPropertyBlock _propBlock;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

    // Timers
    private float _clickTimer;
    private float _transitionTimer;
    private bool _isStageDownTransition;
    private float _hitTimer;
    private Vector3 _hitTiltAxis = Vector3.right;

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
    }

    public void SetActiveVisualRoot(Transform visualRoot)
    {
        // Reset visual lama jika ada
        ResetTransformState();

        _activeVisual = visualRoot;
        _renderers.Clear();

        if (_activeVisual == null) return;

        _baseLocalPos = _activeVisual.localPosition;
        _baseLocalScale = _activeVisual.localScale;
        _baseLocalRot = _activeVisual.localRotation;

        Renderer[] found = _activeVisual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < found.Length; i++)
        {
            Material mat = found[i].sharedMaterial;
            Color col = (mat != null && mat.HasProperty(ColorId)) ? mat.GetColor(ColorId) : Color.white;
            Color rim = (mat != null && mat.HasProperty(RimColorId)) ? mat.GetColor(RimColorId) : Color.clear;
            _renderers.Add(new RendererEntry
            {
                renderer = found[i],
                baseColor = col,
                baseRimColor = rim
            });
        }
    }

    public void PlayClickBounce()
    {
        _clickTimer = _clickDuration;
    }

    public void PlayStageUpTransition()
    {
        _isStageDownTransition = false;
        _transitionTimer = _transitionDuration;
    }

    public void PlayStageDownTransition()
    {
        _isStageDownTransition = true;
        _transitionTimer = _transitionDuration;
    }

    public void PlayEnemyHitRecoil(Vector3 hitDirection)
    {
        _hitTimer = _hitRecoilDuration;

        Vector3 flatDir = new Vector3(hitDirection.x, 0f, hitDirection.z);
        if (flatDir.sqrMagnitude < 0.001f)
            flatDir = Random.insideUnitSphere;
        flatDir.y = 0f;
        flatDir.Normalize();

        // Sumbu rotasi tegak lurus dengan arah pukulan agar pohon miring menjauhi penyerang
        _hitTiltAxis = Vector3.Cross(Vector3.up, flatDir).normalized;
    }

    private void Update()
    {
        if (_activeVisual == null) return;

        bool hasAnyAnim = _clickTimer > 0f || _transitionTimer > 0f || _hitTimer > 0f;
        if (!hasAnyAnim) return;

        Vector3 scaleMult = Vector3.one;
        Vector3 posOffset = Vector3.zero;
        Quaternion rotOffset = Quaternion.identity;
        Color flashTint = Color.white;
        Color rimAdd = Color.clear;

        // 1. Click Squash & Stretch Bounce
        if (_clickTimer > 0f)
        {
            _clickTimer = Mathf.Max(0f, _clickTimer - Time.deltaTime);
            float t = 1f - (_clickTimer / Mathf.Max(0.001f, _clickDuration));
            // Gelombang sinus teredam: menyusut cepat lalu memantul kembali
            float wave = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            float yFactor = Mathf.Lerp(1f, _clickSquashY, Mathf.Max(0f, wave)) + Mathf.Max(0f, -wave) * 0.08f;
            float xzFactor = Mathf.Lerp(1f, _clickStretchXZ, Mathf.Max(0f, wave)) - Mathf.Max(0f, -wave) * 0.04f;
            scaleMult = Vector3.Scale(scaleMult, new Vector3(xzFactor, yFactor, xzFactor));
        }

        // 2. Stage Up / Stage Down Flash + Shake + Pop
        if (_transitionTimer > 0f)
        {
            _transitionTimer = Mathf.Max(0f, _transitionTimer - Time.deltaTime);
            float t = 1f - (_transitionTimer / Mathf.Max(0.001f, _transitionDuration));
            float envelope = 1f - t;

            // Pop-in scale saat model baru muncul
            float pop = 1f + Mathf.Sin(t * Mathf.PI) * 0.14f * envelope;
            scaleMult *= pop;

            // Getaran horizontal singkat
            float shakeMag = (_isStageDownTransition ? _stageDownShakeMagnitude : _stageUpShakeMagnitude) * envelope;
            posOffset += new Vector3(
                Mathf.Sin(Time.time * 65f) * shakeMag,
                0f,
                Mathf.Cos(Time.time * 58f) * shakeMag
            );

            Color targetFlash = _isStageDownTransition ? _stageDownFlashColor : _stageUpFlashColor;
            flashTint = Color.Lerp(Color.white, targetFlash, envelope * 0.85f);
            rimAdd = targetFlash * (envelope * 0.65f);
        }

        // 3. Enemy Hit Directional Tilt + Flash
        if (_hitTimer > 0f)
        {
            _hitTimer = Mathf.Max(0f, _hitTimer - Time.deltaTime);
            float t = 1f - (_hitTimer / Mathf.Max(0.001f, _hitRecoilDuration));
            float recoil = Mathf.Sin(t * Mathf.PI * 1.5f) * (1f - t);

            rotOffset = Quaternion.AngleAxis(recoil * _hitTiltAngle, _hitTiltAxis);
            flashTint = Color.Lerp(flashTint, _hitFlashColor, Mathf.Max(0f, recoil) * 0.8f);
        }

        _activeVisual.localScale = Vector3.Scale(_baseLocalScale, scaleMult);
        _activeVisual.localPosition = _baseLocalPos + posOffset;
        _activeVisual.localRotation = rotOffset * _baseLocalRot;

        ApplyShaderFlash(flashTint, rimAdd, _transitionTimer > 0f || _hitTimer > 0f);
    }

    private void ApplyShaderFlash(Color tintMultiplier, Color rimAddition, bool active)
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            Renderer r = _renderers[i].renderer;
            if (r == null) continue;

            if (!active)
            {
                r.SetPropertyBlock(null);
                continue;
            }

            r.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(ColorId, _renderers[i].baseColor * tintMultiplier);
            _propBlock.SetColor(RimColorId, _renderers[i].baseRimColor + rimAddition);
            r.SetPropertyBlock(_propBlock);
        }
    }

    private void ResetTransformState()
    {
        if (_activeVisual == null) return;
        _activeVisual.localPosition = _baseLocalPos;
        _activeVisual.localScale = _baseLocalScale;
        _activeVisual.localRotation = _baseLocalRot;
        ApplyShaderFlash(Color.white, Color.clear, false);
    }
}