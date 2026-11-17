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
        float duration = _node.Data.Definition.GetPhaseDuration(_node.Data.Stage);
        if (_progress < duration) return;

        // Carry no leftover into the next stage: keeps the phases simple.
        _progress = 0f;
        _node.Data.SetStage(_node.Data.Stage + 1);
    }
}
