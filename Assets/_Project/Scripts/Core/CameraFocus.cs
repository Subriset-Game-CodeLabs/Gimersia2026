using UnityEngine;

public class CameraFocus : MonoBehaviour
{
    // TODO(balance): placeholder pan speed in units per second.
    [SerializeField] private float _panSpeed = 30f;

    private Vector3 _targetPosition;

    private void Awake()
    {
        // Idle camera does no per-frame work.
        enabled = false;
    }

    public void FocusOn(Vector3 worldPoint)
    {
        // Where the camera currently looks on the ground plane.
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        Ray ray = new Ray(transform.position, transform.forward);
        if (!ground.Raycast(ray, out float distance)) return;

        Vector3 currentFocus = ray.GetPoint(distance);
        Vector3 newFocus = new Vector3(worldPoint.x, 0f, worldPoint.z);

        // Shift by the difference, so this also works while a pan is still running.
        _targetPosition = transform.position + (newFocus - currentFocus);
        enabled = true;
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, _targetPosition, _panSpeed * Time.deltaTime);
        if (transform.position == _targetPosition) enabled = false;
    }
}
