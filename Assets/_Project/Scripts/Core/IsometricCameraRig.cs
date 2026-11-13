using UnityEngine;


[RequireComponent(typeof(Camera))]
public class IsometricCameraRig : MonoBehaviour
{
    [SerializeField] private IslandSpawner _island;
    [SerializeField] private Vector2 _angles = new Vector2(30f, 45f); // pitch, yaw
    [SerializeField] private float _distance = 60f;
    [SerializeField] private float _padding = 4f; // extra world units around the island

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _camera.orthographic = true;
    }

    // Start, not Awake: the island spawns in IslandSpawner.Awake, so its blocks exist by now.
    private void Start() => Frame();

    /// <summary>Positions and sizes the camera. Play mode only, since blocks spawn at runtime.</summary>
    [ContextMenu("Frame Island")]
    private void Frame()
    {
        if (_camera == null) _camera = GetComponent<Camera>();

        if (_island == null || !_island.TryGetBounds(out Bounds bounds))
        {
            Debug.LogWarning("IsometricCameraRig: no island blocks to frame yet.", this);
            return;
        }

        Quaternion rotation = Quaternion.Euler(_angles.x, _angles.y, 0f);

        // Back the camera away from the island center along its view direction.
        transform.rotation = rotation;
        transform.position = bounds.center - rotation * Vector3.forward * _distance;

        _camera.orthographicSize = CalculateOrthoSize(bounds, rotation);
    }

    private float CalculateOrthoSize(Bounds bounds, Quaternion rotation)
    {
        // Project the 8 bound corners into camera space and take the largest extent.
        Matrix4x4 worldToCam = Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one).inverse;
        float maxX = 0f, maxY = 0f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = Vector3.Scale(bounds.extents, new Vector3(
                (i & 1) == 0 ? -1 : 1,
                (i & 2) == 0 ? -1 : 1,
                (i & 4) == 0 ? -1 : 1));
            Vector3 local = worldToCam.MultiplyPoint3x4(corner);
            maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
            maxY = Mathf.Max(maxY, Mathf.Abs(local.y));
        }

        // Orthographic size is half the vertical extent; widen for the aspect ratio.
        float sizeFromWidth = (maxX + _padding) / _camera.aspect;
        float sizeFromHeight = maxY + _padding;
        return Mathf.Max(sizeFromWidth, sizeFromHeight);
    }
}
