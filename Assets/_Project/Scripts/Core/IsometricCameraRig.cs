using UnityEngine;


[RequireComponent(typeof(Camera))]
public class IsometricCameraRig : MonoBehaviour
{
    [SerializeField] private IslandDefinition _definition;
    [SerializeField] private Vector2 _angles = new Vector2(30f, 45f); // pitch, yaw
    [SerializeField] private float _distance = 60f;
    [SerializeField] private float _padding = 4f; // extra world units around the island

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _camera.orthographic = true;
        Frame();
    }

    /// <summary>Positions and sizes the camera. Also callable from the context menu.</summary>
    [ContextMenu("Frame Island")]
    private void Frame()
    {
        if (_camera == null) _camera = GetComponent<Camera>();

        Bounds bounds = CalculateBounds();
        Quaternion rotation = Quaternion.Euler(_angles.x, _angles.y, 0f);

        // Back the camera away from the island center along its view direction.
        transform.rotation = rotation;
        transform.position = bounds.center - rotation * Vector3.forward * _distance;

        _camera.orthographicSize = CalculateOrthoSize(bounds, rotation);
    }

    private Bounds CalculateBounds()
    {
        float half = _definition.BlockSize * 0.5f;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;

        foreach (var block in _definition.Blocks)
        {
            Vector3 center = _definition.CellToWorld(block.Cell);
            Bounds blockBounds = new Bounds(center, new Vector3(half * 2f, 0f, half * 2f));
            if (first) { bounds = blockBounds; first = false; }
            else bounds.Encapsulate(blockBounds);
        }
        return bounds;
    }

    private float CalculateOrthoSize(Bounds bounds, Quaternion rotation)
    {
        // Project the 8 bound corners into camera space and take the largest extent.
        Matrix4x4 worldToCam = Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one).inverse;
        float maxX = 0f, maxY = 0f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (i & 1) == 0 ? -1 : 1,
                (i & 2) == 0 ? -1 : 1,
                (i & 4) == 0 ? -1 : 1));
            Vector3 local = worldToCam.MultiplyPoint3x4(corner - bounds.center);
            maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
            maxY = Mathf.Max(maxY, Mathf.Abs(local.y));
        }

        // Orthographic size is half the vertical extent; widen for the aspect ratio.
        float sizeFromWidth = (maxX + _padding) / _camera.aspect;
        float sizeFromHeight = maxY + _padding;
        return Mathf.Max(sizeFromWidth, sizeFromHeight);
    }
}
