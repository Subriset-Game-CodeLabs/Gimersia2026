using UnityEngine;

[RequireComponent(typeof(TreeNode))]
public class TreeElementInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("Isi > 0 untuk mengatur durasi penggantian elemen pohon ini secara langsung (detik). Jika 0, mengikuti ElementSettings.")]
    [SerializeField, Min(0f)] private float _reassignmentDurationOverride = 4f;

    private TreeNode _node;
    private WorkerSet _workers;
    private ElementType _pendingElement = ElementType.None;
    private float _progress;

    public ElementType PendingElement => _pendingElement;
    public bool IsWorking => _workers != null && _workers.Count > 0 && CanInteract;

    public float Duration
    {
        get
        {
            if (_reassignmentDurationOverride > 0f)
                return _reassignmentDurationOverride;

            ElementSettings settings = TreeRegistry.Instance != null
                ? TreeRegistry.Instance.Settings
                : null;
            return settings != null ? settings.ReassignmentDuration : 3f;
        }
    }

    public float Progress01
    {
        get
        {
            float duration = Duration;
            if (duration <= 0f) return CanInteract ? 1f : 0f;
            return Mathf.Clamp01(_progress / duration);
        }
    }

    public Vector3 InteractionPoint => _node.InteractionPoint;

    public bool CanInteract =>
        _node != null &&
        _node.IsTargetable &&
        _node.Data.IsFullyGrown &&
        _node.Data.Element != ElementType.None &&
        _pendingElement != ElementType.None &&
        _pendingElement != _node.Data.Element;

    private void Awake()
    {
        if (_node == null) _node = GetComponent<TreeNode>();
    }

    public void Initialize(TeamworkSettings teamwork)
    {
        _node = GetComponent<TreeNode>();
        _workers = new WorkerSet(teamwork);
    }

    public bool Queue(ElementType element)
    {
        if (_node == null || !_node.IsTargetable ||
            !_node.Data.IsFullyGrown ||
            _node.Data.Element == ElementType.None ||
            element == ElementType.None ||
            element == _node.Data.Element)
            return false;

        if (_pendingElement != element)
        {
            _pendingElement = element;
            _progress = 0f;
        }
        return true;
    }

    public void CancelQueue()
    {
        _pendingElement = ElementType.None;
        _progress = 0f;
        UpdateProgressVfx();
    }

    public void AddWorker(SpiritController spirit)
    {
        if (_workers == null) return;
        _workers.Add(spirit);
        UpdateProgressVfx();
    }

    public void RemoveWorker(SpiritController spirit)
    {
        if (_workers == null) return;
        _workers.Remove(spirit);

        if (_workers.Count == 0 && _progress > 0f)
        {
            CancelQueue();
            return;
        }

        UpdateProgressVfx();
    }

    public void Interact(SpiritController spirit, float deltaTime)
    {
        if (!CanInteract || _workers == null)
        {
            if (_node != null) _node.SetElementChangeVfxActive(false);
            return;
        }

        UpdateProgressVfx();
        _progress += _workers.GetShare(deltaTime);

        if (_progress < Duration)
        {
            AudioManager.Instance.PlayTreeWorkTick(InteractionPoint);
            return;
        }

        if (_node.Data.CompleteReassignment(_pendingElement))
        {
            _pendingElement = ElementType.None;
            _progress = 0f;
        }
        UpdateProgressVfx();
    }

    private void UpdateProgressVfx()
    {
        if (_node != null)
            _node.SetElementChangeVfxActive(IsWorking);
    }

    private void OnDisable()
    {
        CancelQueue();
    }
}