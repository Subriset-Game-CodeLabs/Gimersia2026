using UnityEngine;

public class TreeElementInteractable : MonoBehaviour, IInteractable
{
    private TreeNode _node;
    private WorkerSet _workers;
    private ElementType _pendingElement = ElementType.None;
    private float _progress;
    public ElementType PendingElement => _pendingElement;
    public float Progress01
    {
        get
        {
            ElementSettings settings = TreeRegistry.Instance != null
                ? TreeRegistry.Instance.Settings
                : null;
            if (settings == null || settings.ReassignmentDuration <= 0f)
                return CanInteract ? 1f : 0f;
            return Mathf.Clamp01(_progress / settings.ReassignmentDuration);
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
    public void Initialize(TeamworkSettings teamwork)
    {
        // The spawned tree may be inactive until its block is cleaned.
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
    public void AddWorker(SpiritController spirit)
    {
        _workers.Add(spirit);
    }
    public void RemoveWorker(SpiritController spirit)
    {
        _workers.Remove(spirit);
    }
    public void Interact(SpiritController spirit, float deltaTime)
    {
        if (!CanInteract || _workers == null) return;
        ElementSettings settings = TreeRegistry.Instance != null
            ? TreeRegistry.Instance.Settings
            : null;
        if (settings == null) return;
        _progress += _workers.GetShare(deltaTime);
        if (_progress < settings.ReassignmentDuration) return;
        if (_node.Data.CompleteReassignment(_pendingElement))
        {
            _pendingElement = ElementType.None;
            _progress = 0f;
        }
    }
}
