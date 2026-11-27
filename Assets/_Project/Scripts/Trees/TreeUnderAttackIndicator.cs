using System.Collections.Generic;
using UnityEngine;

public class TreeUnderAttackIndicator : MonoBehaviour
{
    [Header("Red Attack Warning Glow")]
    [SerializeField] private Color _underAttackTint = new Color(1.30f, 0.65f, 0.65f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _underAttackRim = new Color(1.5f, 0.15f, 0.15f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _underAttackEmission = new Color(0.95f, 0.08f, 0.08f, 1f);
    [SerializeField] private float _pulseSpeed = 6f;

    private readonly HashSet<EnemyController> _attackers = new HashSet<EnemyController>();
    private readonly List<EnemyController> _cleanupBuffer = new List<EnemyController>();

    private Renderer[] _renderers;
    private Color[] _baseColors;
    private Color[] _baseRims;
    private Color[] _baseEmissions;
    private MaterialPropertyBlock _propBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        enabled = false;
    }

    public void SetActiveVisualRoot(Transform visualRoot)
    {
        ClearPropertyBlocks();

        if (visualRoot == null)
        {
            _renderers = null;
            return;
        }

        _renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        _baseColors = new Color[_renderers.Length];
        _baseRims = new Color[_renderers.Length];
        _baseEmissions = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null || r.sharedMaterial == null) continue;

            Material mat = r.sharedMaterial;
            _baseColors[i] = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId)
                : mat.HasProperty(ColorId) ? mat.GetColor(ColorId) : Color.white;
            _baseRims[i] = mat.HasProperty(RimColorId) ? mat.GetColor(RimColorId) : Color.clear;
            _baseEmissions[i] = mat.HasProperty(EmissionColorId) ? mat.GetColor(EmissionColorId) : Color.black;
        }
    }

    public void RegisterAttacker(EnemyController enemy)
    {
        if (enemy == null) return;
        _attackers.Add(enemy);
        enabled = true;
    }

    public void UnregisterAttacker(EnemyController enemy)
    {
        if (enemy == null) return;
        _attackers.Remove(enemy);
    }

    private void Update()
    {
        PruneDeadAttackers();

        if (_attackers.Count == 0)
        {
            ClearPropertyBlocks();
            enabled = false;
            return;
        }

        float pulse = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.time * _pulseSpeed));
        ApplyGlow(pulse);
    }

    private void PruneDeadAttackers()
    {
        _cleanupBuffer.Clear();
        foreach (EnemyController enemy in _attackers)
        {
            if (enemy == null || !enemy.IsAlive)
                _cleanupBuffer.Add(enemy);
        }

        for (int i = 0; i < _cleanupBuffer.Count; i++)
            _attackers.Remove(_cleanupBuffer[i]);
    }

    private void ApplyGlow(float intensity)
    {
        if (_renderers == null || _propBlock == null) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null || r.sharedMaterial == null) continue;

            r.GetPropertyBlock(_propBlock);
            Color tint = Color.Lerp(_baseColors[i], _baseColors[i] * _underAttackTint, intensity);
            Color rim = Color.Lerp(_baseRims[i], _underAttackRim, intensity);
            Color emission = Color.Lerp(_baseEmissions[i], _underAttackEmission, intensity);

            _propBlock.SetColor(BaseColorId, tint);
            _propBlock.SetColor(ColorId, tint);
            _propBlock.SetColor(RimColorId, rim);
            _propBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(_propBlock);
        }
    }

    private void ClearPropertyBlocks()
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].SetPropertyBlock(null);
        }
    }

    private void OnDisable()
    {
        ClearPropertyBlocks();
    }
}