using UnityEngine;

public class GridInteractable : MonoBehaviour, IInteractable
{
    // TODO(balance): placeholder cleaning time in seconds.
    [SerializeField] private float _cleanDuration = 3f;

    private GridBlockData _block;
    private float _progress;
    private BlockView _view;
    private WorkerSet _workers;

    private void Awake()
    {
        // Cached once; the spawner calls Initialize after this runs.
        _view = GetComponent<BlockView>();
    }

    public Vector3 InteractionPoint => _view.CleaningPoint;

    // Only Corrupted blocks need work.
    public bool CanInteract => _block != null && _workers != null && _block.State == GridState.Corrupted;

    // Called by IslandSpawner right after the view is created.
    public void Initialize(GridBlockData block, TeamworkSettings teamwork)
    {
        _block = block;
        _progress = 0f;
        _workers = new WorkerSet(teamwork);
    }

    public void AddWorker(SpiritController spirit) => _workers.Add(spirit);
    public void RemoveWorker(SpiritController spirit) => _workers.Remove(spirit);

    public void Interact(SpiritController spirit, float deltaTime)
    {
        if (!CanInteract) return;
        _progress += _workers.GetShare(deltaTime);
        if (_progress >= _cleanDuration)
        {
            // BlockView listens to OnStateChanged and recolors itself.
            _block.SetState(GridState.Clean);
        }
        else
        {
            AudioManager.Instance.PlayBlockCleaningTick(InteractionPoint);
        }
    }
}