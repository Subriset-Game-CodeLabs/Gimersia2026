using UnityEngine;


[RequireComponent(typeof(TreeNode))]
public class TreeInteractable : MonoBehaviour, IInteractable
{
    private TreeNode _node;
    private WorkerSet _workers;
    private float _progress; // work done in the current stage, in seconds

    // 0..1 for the current stage. A progress bar can read this later.
    public float Progress01
    {
        get
        {
            float duration = _node.Data.Definition.GetPhaseDuration(_node.Data.Stage);
            return duration > 0f ? Mathf.Clamp01(_progress / duration) : 1f;
        }
    }

    private void Awake()
    {
        _node = GetComponent<TreeNode>();
    }

    // Called by IslandSpawner right after the node is created.
    public void Initialize(TeamworkSettings teamwork)
    {
        _workers = new WorkerSet(teamwork);
    }

    public Vector3 InteractionPoint => transform.position;

    // Hidden nodes are inactive, so they cannot be clicked. This covers the rest.
    public bool CanInteract => _node.Data != null && _node.Data.IsAlive && !_node.Data.IsFullyGrown;

    public void AddWorker(SpiritController spirit) => _workers.Add(spirit);
    public void RemoveWorker(SpiritController spirit) => _workers.Remove(spirit);

    public void Interact(SpiritController spirit, float deltaTime)
    {
        if (!CanInteract) return;
        _progress += _workers.GetShare(deltaTime);
        if (_progress < GetCurrentDuration()) return;
        _progress = 0f;
        _node.Data.SetStage(_node.Data.Stage + 1);
    }

    private float GetCurrentDuration()
    {
        float baseDuration =
            _node.Data.Definition.GetPhaseDuration(_node.Data.Stage);
        TreeRegistry registry = TreeRegistry.Instance;
        float multiplier = registry != null
            ? registry.GetGrowthTimeMultiplier(_node.Data.Element)
            : 1f;
        return baseDuration * multiplier;
    }
}
