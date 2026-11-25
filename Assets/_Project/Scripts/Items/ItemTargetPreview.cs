using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class ItemTargetPreview : MonoBehaviour
{
    [SerializeField] private LineRenderer _line;
    [SerializeField, Range(16, 128)] private int _segments = 48;
    [SerializeField, Min(0f)] private float _heightOffset = 0.06f;
    [SerializeField, Min(0.01f)] private float _width = 0.06f;
    [SerializeField] private Color _validColor = new Color(0.25f, 1f, 0.45f);
    [SerializeField] private Color _invalidColor = new Color(1f, 0.25f, 0.2f);

    public bool IsConfigured => _line != null;

    private void Awake()
    {
        if (_line == null) _line = GetComponent<LineRenderer>();
        if (_line == null) return;

        _segments = Mathf.Clamp(_segments, 16, 128);
        _line.useWorldSpace = true;
        _line.loop = true;
        _line.positionCount = _segments;
        _line.startWidth = _line.endWidth = Mathf.Max(0.01f, _width);
        Hide();
    }

    private void OnDisable() => Hide();

    public void Show(Vector3 center, float radius, bool valid)
    {
        if (_line == null) return;

        Color color = valid ? _validColor : _invalidColor;
        _line.startColor = _line.endColor = color;
        for (int i = 0; i < _segments; i++)
        {
            float angle = i * (Mathf.PI * 2f / _segments);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, _heightOffset, Mathf.Sin(angle) * radius);
            _line.SetPosition(i, center + offset);
        }
        _line.enabled = true;
    }

    public void Hide()
    {
        if (_line != null) _line.enabled = false;
    }
}
