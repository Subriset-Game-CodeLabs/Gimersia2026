using System.Collections.Generic;
using UnityEngine;

public class BlockView : MonoBehaviour
{
    [SerializeField] private Renderer _tileRenderer;

    [SerializeField, HideInInspector] private Collider _tileCollider;
    [SerializeField] private List<Collider> _tileColliders = new List<Collider>();
    [SerializeField] private Transform _cleaningPoint;
    [SerializeField] private GameObject _dirtyVisualsRoot;
    [Tooltip("Isi dengan 1, 2, atau 3 bagian sampah sesuai kebutuhan blok ini. Jika dikosongkan semua, otomatis mengambil semua child langsung di dalam Dirty Visuals Root.")]
    [SerializeField] private GameObject[] _dirtyStageGroups = new GameObject[3];
    [SerializeField] private Renderer[] _groundRenderers;

    [Header("Hover & Click Polish")]
    [Tooltip("Opsional: Jika dikosongkan, otomatis mengangkat seluruh Ground Renderers dan Dirty Visuals secara bersamaan.")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private float _hoverLiftHeight = 0.10f;
    [SerializeField] private float _hoverSmoothSpeed = 16f;
    [SerializeField] private float _clickDipDepth = 0.06f;
    [SerializeField] private float _clickDipDuration = 0.22f;
    [SerializeField] private Color _corruptedHoverTint = new Color(0.85f, 1.15f, 1.25f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _corruptedHoverRim = new Color(0.25f, 0.95f, 1.1f, 0.85f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _clickConfirmRim = new Color(0.5f, 1.3f, 1.4f, 1f);

    private bool _isHighlighted;
    private IslandTile[] _tiles;
    private Bounds _groundBounds;
    private bool _hasBounds;

    public IReadOnlyList<Collider> TileColliders => _tileColliders;
    public Vector3 CleaningPoint => _cleaningPoint != null ? _cleaningPoint.position : transform.position;
    public IReadOnlyList<IslandTile> Tiles => _tiles;
    public Renderer TileRenderer => _tileRenderer;

    private GridBlockData _data;
    private MaterialPropertyBlock _propBlock;
    private Color[] _baseMainColors;
    private Color[] _baseRimColors;
    private GameObject[] _resolvedStageGroups;

    // Visual lift & click feedback state
    private Vector3 _visualRootBaseLocalPos;
    private Vector3[] _groundBaseLocalPositions;
    private Vector3 _dirtyVisualsBaseLocalPos;
    private float _currentLiftY;
    private float _clickDipTimer;

    private struct TrackedOrder
    {
        public SpiritController spirit;
        public int commandId;
    }
    private readonly List<TrackedOrder> _trackedOrders = new List<TrackedOrder>();

    public GridBlockData Data => _data;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

    public void Initialize(GridBlockData data)
    {
        _data = data;
        CacheGroundAppearance();
        _data.OnStateChanged += HandleStateChanged;
        _data.OnCleanProgressChanged += HandleCleanProgressChanged;
        foreach (IslandTile tile in _tiles) tile.Initialize(this);
        RefreshState();

        bool hasCollider = false;
        foreach (Collider collider in _tileColliders)
            if (collider != null) hasCollider = true;
        if (_cleaningPoint == null || !hasCollider)
            Debug.LogError("BlockView needs a Cleaning Point and at least one whole-block click collider in Tile Colliders.", this);
    }

    private void OnDestroy()
    {
        if (_data != null)
        {
            _data.OnStateChanged -= HandleStateChanged;
            _data.OnCleanProgressChanged -= HandleCleanProgressChanged;
        }
    }

    private void HandleCleanProgressChanged(GridBlockData _)
    {
        if (_data == null) return;
        ApplyDirtyStageVisuals(_data.State == GridState.Clean, _data.CleanProgress);
    }

    private void HandleStateChanged(GridBlockData _)
    {
        if (_data != null && _data.State == GridState.Clean && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBlockCleaned(CleaningPoint);
        }
        RefreshState();
    }

    private void RefreshState()
    {
        bool clean = _data.State == GridState.Clean;
        foreach (Collider collider in _tileColliders)
            if (collider != null) collider.enabled = !clean;

        ApplyDirtyStageVisuals(clean, _data.CleanProgress);

        foreach (IslandTile tile in _tiles) tile.SetSelectable(clean);

        _isHighlighted = false;
        _clickDipTimer = 0f;
        _currentLiftY = 0f;
        _trackedOrders.Clear();

        ApplyLiftOffset(0f);
        ApplyVisualState(0f, 0f);
        enabled = false;
    }

    private void ApplyDirtyStageVisuals(bool isClean, float normalizedProgress)
    {
        if (_dirtyVisualsRoot != null)
            _dirtyVisualsRoot.SetActive(!isClean);

        if (_resolvedStageGroups == null || _resolvedStageGroups.Length == 0)
            return;

        int stageCount = _resolvedStageGroups.Length;
        int clearedCount = isClean
            ? stageCount
            : Mathf.Clamp(Mathf.FloorToInt(normalizedProgress * stageCount), 0, stageCount - 1);

        for (int i = 0; i < stageCount; i++)
        {
            if (_resolvedStageGroups[i] != null)
                _resolvedStageGroups[i].SetActive(!isClean && i >= clearedCount);
        }
    }

    private void ResolveDirtyStageGroups()
    {
        var list = new List<GameObject>();

        if (_dirtyStageGroups != null)
        {
            for (int i = 0; i < _dirtyStageGroups.Length; i++)
            {
                if (_dirtyStageGroups[i] != null)
                    list.Add(_dirtyStageGroups[i]);
            }
        }

        if (list.Count == 0 && _dirtyVisualsRoot != null)
        {
            Transform root = _dirtyVisualsRoot.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                list.Add(root.GetChild(i).gameObject);
            }
        }

        _resolvedStageGroups = list.ToArray();
    }

    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = _groundBounds;
        return _hasBounds;
    }

    private void CacheGroundAppearance()
    {
        _propBlock = new MaterialPropertyBlock();
        _baseMainColors = new Color[_groundRenderers.Length];
        _baseRimColors = new Color[_groundRenderers.Length];

        for (int i = 0; i < _groundRenderers.Length; i++)
        {
            Renderer ground = _groundRenderers[i];
            if (ground == null) continue;

            Material material = ground.sharedMaterial;
            if (material != null)
            {
                _baseMainColors[i] = material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId)
                    : material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
                _baseRimColors[i] = material.HasProperty(RimColorId) ? material.GetColor(RimColorId) : Color.clear;
            }
            else
            {
                _baseMainColors[i] = Color.white;
                _baseRimColors[i] = Color.clear;
            }
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (_isHighlighted == highlighted) return;
        _isHighlighted = highlighted;
        enabled = true;
    }

    public void TriggerClickFeedback(IReadOnlyList<SpiritView> dispatchedSpirits)
    {
        if (_data == null || _data.State != GridState.Corrupted) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayTileCommandClick(CleaningPoint);

        _clickDipTimer = _clickDipDuration;
        _trackedOrders.Clear();

        if (dispatchedSpirits != null)
        {
            for (int i = 0; i < dispatchedSpirits.Count; i++)
            {
                if (dispatchedSpirits[i] == null) continue;
                SpiritController ctrl = dispatchedSpirits[i].GetComponent<SpiritController>();
                if (ctrl != null)
                {
                    _trackedOrders.Add(new TrackedOrder
                    {
                        spirit = ctrl,
                        commandId = ctrl.CommandId
                    });
                }
            }
        }

        enabled = true;
    }

    private bool HasActiveDestinationOrders()
    {
        for (int i = _trackedOrders.Count - 1; i >= 0; i--)
        {
            TrackedOrder order = _trackedOrders[i];
            if (order.spirit == null ||
                order.spirit.CommandId != order.commandId ||
                (order.spirit.State != SpiritState.Moving && order.spirit.State != SpiritState.Interacting))
            {
                _trackedOrders.RemoveAt(i);
            }
        }
        return _trackedOrders.Count > 0;
    }

    private void Update()
    {
        bool hasDestination = HasActiveDestinationOrders();
        float targetY = _isHighlighted ? _hoverLiftHeight : 0f;

        if (_clickDipTimer > 0f)
        {
            _clickDipTimer = Mathf.Max(0f, _clickDipTimer - Time.deltaTime);
            float dipT = 1f - (_clickDipTimer / Mathf.Max(0.001f, _clickDipDuration));
            targetY -= Mathf.Sin(dipT * Mathf.PI) * _clickDipDepth;
        }

        _currentLiftY = Mathf.MoveTowards(_currentLiftY, targetY, _hoverSmoothSpeed * Time.deltaTime);
        ApplyLiftOffset(_currentLiftY);

        float hoverBlend = _isHighlighted ? 1f : 0f;
        float pulseBlend = 0f;

        if (_clickDipTimer > 0f)
        {
            float flashT = _clickDipTimer / Mathf.Max(0.001f, _clickDipDuration);
            pulseBlend = Mathf.Max(pulseBlend, flashT);
        }
        if (hasDestination)
        {
            float wave = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.time * 5f));
            pulseBlend = Mathf.Max(pulseBlend, wave);
        }

        ApplyVisualState(hoverBlend, pulseBlend);

        if (!_isHighlighted && !hasDestination && _clickDipTimer <= 0f && Mathf.Abs(_currentLiftY) <= 0.0001f)
        {
            _currentLiftY = 0f;
            ApplyLiftOffset(0f);
            ApplyVisualState(0f, 0f);
            enabled = false;
        }
    }

    private void ApplyLiftOffset(float liftY)
    {
        Vector3 offset = new Vector3(0f, liftY, 0f);

        if (_visualRoot != null && _visualRoot != transform)
        {
            _visualRoot.localPosition = _visualRootBaseLocalPos + offset;
            return;
        }

        if (_groundRenderers != null && _groundBaseLocalPositions != null)
        {
            for (int i = 0; i < _groundRenderers.Length; i++)
            {
                Renderer ground = _groundRenderers[i];
                if (ground != null && ground.transform != transform)
                {
                    ground.transform.localPosition = _groundBaseLocalPositions[i] + offset;
                }
            }
        }

        if (_dirtyVisualsRoot != null && _dirtyVisualsRoot.transform != transform)
        {
            _dirtyVisualsRoot.transform.localPosition = _dirtyVisualsBaseLocalPos + offset;
        }
    }

    private void ApplyVisualState(float hoverBlend, float pulseBlend)
    {
        if (_baseMainColors == null || _propBlock == null) return;

        float totalGlow = Mathf.Clamp01(Mathf.Max(hoverBlend, pulseBlend));

        for (int i = 0; i < _groundRenderers.Length; i++)
        {
            Renderer ground = _groundRenderers[i];
            if (ground == null) continue;

            if (totalGlow <= 0.001f)
            {
                ground.SetPropertyBlock(null);
                continue;
            }

            ground.GetPropertyBlock(_propBlock);

            Color tinted = Color.Lerp(_baseMainColors[i], _baseMainColors[i] * _corruptedHoverTint, totalGlow);
            Color targetRim = Color.Lerp(_corruptedHoverRim, _clickConfirmRim, pulseBlend);
            Color finalRim = Color.Lerp(_baseRimColors[i], targetRim, totalGlow);

            _propBlock.SetColor(BaseColorId, tinted);
            _propBlock.SetColor(ColorId, tinted);
            _propBlock.SetColor(RimColorId, finalRim);
            ground.SetPropertyBlock(_propBlock);
        }
    }

    private void Awake()
    {
        if (_tileColliders == null) _tileColliders = new List<Collider>();
        if (_tileColliders.Count == 0 && _tileCollider != null)
            _tileColliders.Add(_tileCollider);

        ResolveDirtyStageGroups();

        _tiles = GetComponentsInChildren<IslandTile>();

        if (_groundRenderers == null || _groundRenderers.Length == 0)
            _groundRenderers = _tileRenderer != null ? new[] { _tileRenderer } : new Renderer[0];

        _groundBaseLocalPositions = new Vector3[_groundRenderers.Length];
        for (int i = 0; i < _groundRenderers.Length; i++)
        {
            Renderer ground = _groundRenderers[i];
            if (ground == null) continue;

            _groundBaseLocalPositions[i] = ground.transform.localPosition;
            if (!_hasBounds) { _groundBounds = ground.bounds; _hasBounds = true; }
            else _groundBounds.Encapsulate(ground.bounds);
        }

        if (_visualRoot != null && _visualRoot != transform)
            _visualRootBaseLocalPos = _visualRoot.localPosition;

        if (_dirtyVisualsRoot != null && _dirtyVisualsRoot.transform != transform)
            _dirtyVisualsBaseLocalPos = _dirtyVisualsRoot.transform.localPosition;

        enabled = false;
    }
}