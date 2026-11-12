using UnityEngine;

public class BlockView : MonoBehaviour
{
    [SerializeField] private Renderer _tileRenderer;
    [SerializeField] private Color _corruptedColor = new Color(0.35f, 0.2f, 0.4f);
    [SerializeField] private Color _cleanColor = new Color(0.35f, 0.7f, 0.3f);

    [SerializeField] private float _highlightAmount = 0.35f; // how much to lighten the tile
    private bool _isHighlighted;

    [SerializeField] private Collider _tileCollider;
    public Collider TileCollider => _tileCollider;

    private GridBlockData _data;
    private MaterialPropertyBlock _propertyBlock;

    public GridBlockData Data => _data;

    // Unity's built-in/URP shaders use different color property names.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public void Initialize(GridBlockData data)
    {
        _data = data;
        _propertyBlock = new MaterialPropertyBlock();
        _data.OnStateChanged += HandleStateChanged;
        ApplyColor();
    }

    private void OnDestroy()
    {
        if (_data != null) _data.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GridBlockData _) => ApplyColor();

    private void ApplyColor()
    {
        Color color = _data.State == GridState.Corrupted ? _corruptedColor : _cleanColor;
        if (_isHighlighted) color = Color.Lerp(color, Color.white, _highlightAmount);

        // Property block avoids creating a material instance per tile.
        _tileRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(BaseColorId, color);
        _propertyBlock.SetColor(ColorId, color);
        _tileRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>Turns the hover highlight on or off.</summary>
    public void SetHighlighted(bool highlighted)
    {
        if (_isHighlighted == highlighted) return;
        _isHighlighted = highlighted;
        ApplyColor();
    }
}
