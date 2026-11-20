using UnityEngine;

[DisallowMultipleComponent]
public class IslandTile : MonoBehaviour
{
    [SerializeField] private Collider _clickCollider;
    [SerializeField] private Renderer _highlightRenderer;
    [SerializeField] private Transform _movementPoint;
    [SerializeField] private TreeDefinition _treeDefinition;
    [SerializeField] private Transform _treePoint;
    [SerializeField] private Transform _interactionPoint;
    [SerializeField, Range(0f, 1f)] private float _highlightAmount = 0.35f;

    private BlockView _block;
    private bool _selectable;
    private bool _highlighted;
    private MaterialPropertyBlock _properties;
    private Color _baseColor;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public Collider ClickCollider => _clickCollider;
    public TreeDefinition TreeDefinition => _treeDefinition;
    public Transform TreePoint => _treePoint;
    public Transform InteractionPoint => _interactionPoint;
    public Vector3 MovementPoint => _movementPoint != null ? _movementPoint.position : transform.position;
    public TreeNode Tree { get; private set; }
    public bool IsSelectable => _selectable && _block != null && _block.Data.State == GridState.Clean;
    // Tree-assigned tiles remain reserved, even after their tree dies.
    public bool CanMoveTo => IsSelectable && _treeDefinition == null;

    public void Initialize(BlockView block)
    {
        _block = block;
        _properties = new MaterialPropertyBlock();
        if (_highlightRenderer != null && _highlightRenderer.sharedMaterial != null)
        {
            Material material = _highlightRenderer.sharedMaterial;
            _baseColor = material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId)
                : material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
        }
        if (_clickCollider == null)
            Debug.LogError("IslandTile needs a dedicated click collider.", this);
        if (_treeDefinition != null && (_treePoint == null || _interactionPoint == null))
            Debug.LogError("Tree tiles need Tree Point and an Interaction Point outside the tree obstacle.", this);
    }

    public void SetTree(TreeNode tree) => Tree = tree;

    public void SetSelectable(bool selectable)
    {
        SetHighlighted(false);
        _selectable = selectable;
        if (_clickCollider != null) _clickCollider.enabled = selectable;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (_highlighted == highlighted || _highlightRenderer == null || _properties == null) return;
        _highlighted = highlighted;
        _highlightRenderer.GetPropertyBlock(_properties);
        if (highlighted)
        {
            // Save the normal tint so leaving hover cannot accumulate brightness.
            Color color = _properties.HasColor(BaseColorId) ? _properties.GetColor(BaseColorId) : _baseColor;
            _baseColor = color;
            float brightness = 1f + Mathf.Max(0f, _highlightAmount);
            color = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
            _properties.SetColor(BaseColorId, color);
            _properties.SetColor(ColorId, color);
        }
        else
        {
            _properties.SetColor(BaseColorId, _baseColor);
            _properties.SetColor(ColorId, _baseColor);
        }
        _highlightRenderer.SetPropertyBlock(_properties);
    }
}
