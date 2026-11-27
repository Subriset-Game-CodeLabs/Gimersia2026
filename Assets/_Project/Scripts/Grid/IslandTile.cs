using System.Collections.Generic;
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

    [Header("Hover Elevation & Visual Polish")]
    [Tooltip("Opsional: Jika dikosongkan, otomatis mengangkat objek tile ini sambil menjaga posisi Click Collider tetap diam.")]
    [SerializeField] private Transform _visualTransform;
    [SerializeField] private float _hoverLiftHeight = 0.12f;
    [SerializeField] private float _hoverSmoothSpeed = 18f;
    [SerializeField] private float _clickDipDepth = 0.055f;
    [SerializeField] private float _clickDipDuration = 0.22f;

    [Header("Contextual Hover Colors (Move vs Tree)")]
    [SerializeField] private Color _moveHoverTint = new Color(0.88f, 1.18f, 0.90f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _moveHoverRim = new Color(0.35f, 1.05f, 0.45f, 0.85f);
    [SerializeField] private Color _treeHoverTint = new Color(1.20f, 1.05f, 0.78f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _treeHoverRim = new Color(1.15f, 0.85f, 0.22f, 0.90f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _clickConfirmRim = new Color(1.35f, 1.35f, 0.75f, 1f);

    private BlockView _block;
    private bool _selectable;
    private bool _highlighted;
    private MaterialPropertyBlock _properties;
    private Color _baseColor = Color.white;
    private Color _baseRimColor = Color.clear;

    // Elevation & feedback state
    private Transform _liftTarget;
    private Vector3 _liftBaseLocalPos;
    private bool _compensateChildCollider;
    private Vector3 _colliderBaseLocalPos;
    private float _currentLiftY;
    private float _clickDipTimer;

    private struct TrackedOrder
    {
        public SpiritController spirit;
        public int commandId;
    }
    private readonly List<TrackedOrder> _trackedOrders = new List<TrackedOrder>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

    public Collider ClickCollider => _clickCollider;
    public TreeDefinition TreeDefinition => _treeDefinition;
    public Transform TreePoint => _treePoint;
    public Transform InteractionPoint => _interactionPoint;
    public Vector3 MovementPoint => _movementPoint != null ? _movementPoint.position : transform.position;
    public TreeNode Tree { get; private set; }
    public bool IsSelectable => _selectable && _block != null && _block.Data.State == GridState.Clean;
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
            _baseRimColor = material.HasProperty(RimColorId) ? material.GetColor(RimColorId) : Color.clear;
        }

        if (_visualTransform != null && _visualTransform != transform)
        {
            _liftTarget = _visualTransform;
            _compensateChildCollider = false;
        }
        else if (_highlightRenderer != null && _highlightRenderer.transform != transform)
        {
            _liftTarget = _highlightRenderer.transform;
            _compensateChildCollider = false;
        }
        else
        {
            // Mengangkat root tile secara langsung dan menahan ClickTarget child agar raycast tidak berkedip
            _liftTarget = transform;
            _compensateChildCollider = _clickCollider != null
                && _clickCollider.transform != transform
                && _clickCollider.transform.IsChildOf(transform);
        }

        _liftBaseLocalPos = _liftTarget.localPosition;
        if (_compensateChildCollider)
            _colliderBaseLocalPos = _clickCollider.transform.localPosition;

        if (_clickCollider == null)
            Debug.LogError("IslandTile needs a dedicated click collider.", this);
        if (_treeDefinition != null && (_treePoint == null || _interactionPoint == null))
            Debug.LogError("Tree tiles need Tree Point and an Interaction Point outside the tree obstacle.", this);

        enabled = false;
    }

    public void SetTree(TreeNode tree) => Tree = tree;

    public void SetSelectable(bool selectable)
    {
        _selectable = selectable;
        if (_clickCollider != null) _clickCollider.enabled = selectable;

        if (!selectable)
        {
            _highlighted = false;
            _clickDipTimer = 0f;
            _currentLiftY = 0f;
            _trackedOrders.Clear();
            ApplyLiftOffset(0f);
            ApplyVisual(0f, 0f);
            enabled = false;
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (_highlighted == highlighted) return;
        _highlighted = highlighted;
        enabled = true;
    }

    public void TriggerClickFeedback(IReadOnlyList<SpiritView> dispatchedSpirits)
    {
        if (!IsSelectable) return;

        AudioManager.Instance.PlayTileCommandClick(MovementPoint);

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
        float targetY = _highlighted ? _hoverLiftHeight : 0f;

        if (_clickDipTimer > 0f)
        {
            _clickDipTimer = Mathf.Max(0f, _clickDipTimer - Time.deltaTime);
            float dipT = 1f - (_clickDipTimer / Mathf.Max(0.001f, _clickDipDuration));
            targetY -= Mathf.Sin(dipT * Mathf.PI) * _clickDipDepth;
        }

        _currentLiftY = Mathf.MoveTowards(_currentLiftY, targetY, _hoverSmoothSpeed * Time.deltaTime);
        ApplyLiftOffset(_currentLiftY);

        float hoverBlend = _highlighted ? 1f : 0f;
        float pulseBlend = 0f;

        if (_clickDipTimer > 0f)
        {
            float flashT = _clickDipTimer / Mathf.Max(0.001f, _clickDipDuration);
            pulseBlend = Mathf.Max(pulseBlend, flashT);
        }
        if (hasDestination)
        {
            float wave = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.time * 5.5f));
            pulseBlend = Mathf.Max(pulseBlend, wave);
        }

        ApplyVisual(hoverBlend, pulseBlend);

        if (!_highlighted && !hasDestination && _clickDipTimer <= 0f && Mathf.Abs(_currentLiftY) <= 0.0001f)
        {
            _currentLiftY = 0f;
            ApplyLiftOffset(0f);
            ApplyVisual(0f, 0f);
            enabled = false;
        }
    }

    private void ApplyLiftOffset(float liftY)
    {
        if (_liftTarget == null) return;

        Vector3 offset = new Vector3(0f, liftY, 0f);
        _liftTarget.localPosition = _liftBaseLocalPos + offset;

        if (_compensateChildCollider && _clickCollider != null)
        {
            _clickCollider.transform.localPosition = _colliderBaseLocalPos - offset;
        }
    }

    private void ApplyVisual(float hoverBlend, float pulseBlend)
    {
        if (_highlightRenderer == null || _properties == null) return;

        float totalGlow = Mathf.Clamp01(Mathf.Max(hoverBlend, pulseBlend));
        if (totalGlow <= 0.001f)
        {
            _highlightRenderer.SetPropertyBlock(null);
            return;
        }

        bool isTreeTile = _treeDefinition != null;
        Color activeTint = isTreeTile ? _treeHoverTint : _moveHoverTint;
        Color activeRim = isTreeTile ? _treeHoverRim : _moveHoverRim;

        Color tinted = Color.Lerp(_baseColor, _baseColor * activeTint, totalGlow);
        Color targetRim = Color.Lerp(activeRim, _clickConfirmRim, pulseBlend);
        Color finalRim = Color.Lerp(_baseRimColor, targetRim, totalGlow);

        _highlightRenderer.GetPropertyBlock(_properties);
        _properties.SetColor(BaseColorId, tinted);
        _properties.SetColor(ColorId, tinted);
        _properties.SetColor(RimColorId, finalRim);
        _highlightRenderer.SetPropertyBlock(_properties);
    }
}