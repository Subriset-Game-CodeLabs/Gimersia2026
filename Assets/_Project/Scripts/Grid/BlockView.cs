using System.Collections.Generic;
using UnityEngine;

public class BlockView : MonoBehaviour
{
    [SerializeField] private Renderer _tileRenderer;

    [SerializeField] private float _highlightAmount = 0.35f; // how much to lighten the tile
    private bool _isHighlighted;

    // Retain existing prefab assignments while the Inspector moves to a list.
    [SerializeField, HideInInspector] private Collider _tileCollider;
    [SerializeField] private List<Collider> _tileColliders = new List<Collider>();
    [SerializeField] private Transform _cleaningPoint;
    [SerializeField] private GameObject _dirtyVisualsRoot;
    [SerializeField] private Renderer[] _groundRenderers;
    private IslandTile[] _tiles;
    private Bounds _groundBounds;
    private bool _hasBounds;
    public IReadOnlyList<Collider> TileColliders => _tileColliders;
    public Vector3 CleaningPoint => _cleaningPoint != null ? _cleaningPoint.position : transform.position;
    public IReadOnlyList<IslandTile> Tiles => _tiles;

    public Renderer TileRenderer => _tileRenderer;
    
    private GridBlockData _data;
    private MaterialPropertyBlock[] _groundOriginalProperties;
    private MaterialPropertyBlock[] _groundHighlightProperties;

    // Empty child Transforms placed in the prefab, one per tree node slot.
    [SerializeField] private Transform _slotsRoot;
    private Transform[] _slots;

    public IReadOnlyList<Transform> Slots => _slots;

    public GridBlockData Data => _data;

    // Unity's built-in/URP shaders use different color property names.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public void Initialize(GridBlockData data)
    {
        _data = data;
        CacheGroundAppearance();
        _data.OnStateChanged += HandleStateChanged;
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
        if (_data != null) _data.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GridBlockData _) => RefreshState();

    private void RefreshState()
    {
        bool clean = _data.State == GridState.Clean;
        foreach (Collider collider in _tileColliders)
            if (collider != null) collider.enabled = !clean;
        if (_dirtyVisualsRoot != null) _dirtyVisualsRoot.SetActive(!clean);
        foreach (IslandTile tile in _tiles) tile.SetSelectable(clean);
        _isHighlighted = false;
        ApplyColor();
    }

    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = _groundBounds;
        return _hasBounds;
    }

    private void ApplyColor()
    {
        if (_groundOriginalProperties == null) return;
        for (int i = 0; i < _groundRenderers.Length; i++)
        {
            Renderer ground = _groundRenderers[i];
            if (ground == null) continue;
            ground.SetPropertyBlock(
                _isHighlighted ? _groundHighlightProperties[i] : _groundOriginalProperties[i]);
        }
    }

    private void CacheGroundAppearance()
    {
        _groundOriginalProperties = new MaterialPropertyBlock[_groundRenderers.Length];
        _groundHighlightProperties = new MaterialPropertyBlock[_groundRenderers.Length];
        for (int i = 0; i < _groundRenderers.Length; i++)
        {
            Renderer ground = _groundRenderers[i];
            if (ground == null) continue;
            var original = new MaterialPropertyBlock();
            var highlight = new MaterialPropertyBlock();
            ground.GetPropertyBlock(original);
            ground.GetPropertyBlock(highlight);
            Material material = ground.sharedMaterial;
            if (material != null)
            {
                SetHoverColor(original, highlight, material, BaseColorId);
                SetHoverColor(original, highlight, material, ColorId);
            }
            _groundOriginalProperties[i] = original;
            _groundHighlightProperties[i] = highlight;
        }
    }

    private void SetHoverColor(MaterialPropertyBlock original, MaterialPropertyBlock highlight,
        Material material, int propertyId)
    {
        if (!material.HasProperty(propertyId)) return;
        Color color = original.HasColor(propertyId) ? original.GetColor(propertyId) : material.GetColor(propertyId);
        // A white texture tint needs HDR brightness; lerping white to white is invisible.
        float brightness = 1f + Mathf.Max(0f, _highlightAmount);
        highlight.SetColor(propertyId,
            new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a));
    }

    /// <summary>Turns the hover highlight on or off.</summary>
    public void SetHighlighted(bool highlighted)
    {
        if (_isHighlighted == highlighted) return;
        _isHighlighted = highlighted;
        ApplyColor();
    }

    private void Awake()
    {
        // Use the old single assignment only until an explicit list is configured.
        if (_tileColliders == null) _tileColliders = new List<Collider>();
        if (_tileColliders.Count == 0 && _tileCollider != null)
            _tileColliders.Add(_tileCollider);
        // Inactive authoring tiles are omitted, including their tree objectives.
        _tiles = GetComponentsInChildren<IslandTile>();
        // Cache before trees are spawned, excluding decoration renderers from bounds.
        if (_groundRenderers == null || _groundRenderers.Length == 0)
            _groundRenderers = _tileRenderer != null ? new[] { _tileRenderer } : new Renderer[0];
        foreach (Renderer ground in _groundRenderers)
        {
            if (ground == null) continue;
            if (!_hasBounds) { _groundBounds = ground.bounds; _hasBounds = true; }
            else _groundBounds.Encapsulate(ground.bounds);
        }
        int count = _slotsRoot != null ? _slotsRoot.childCount : 0;
        _slots = new Transform[count];
        for (int i = 0; i < count; i++)
            _slots[i] = _slotsRoot.GetChild(i);
    }
}
