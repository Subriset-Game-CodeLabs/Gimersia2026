using UnityEngine;

[RequireComponent(typeof(TreeNode))]
public class TreeInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("Jika aktif, Spirit berhenti setelah menaikkan 1 fase (Initial -> Intermediate) dan perlu diklik lagi untuk fase berikutnya.")]
    [SerializeField] private bool _stopAfterEachStage = true;

    private TreeNode _node;
    private WorkerSet _workers;
    private float _progress;
    private bool _awaitingNewCommand;
    private PhaseManager _phases;

    public float Progress01
    {
        get
        {
            if (_node == null || _node.Data == null) return 0f;
            float duration = GetCurrentDuration();
            return duration > 0f ? Mathf.Clamp01(_progress / duration) : 1f;
        }
    }

    public bool IsWorking => _workers != null && _workers.Count > 0 && CanInteract;

    private void Awake()
    {
        _node = GetComponent<TreeNode>();
    }

    private void Start()
    {
        // Island generation may initialize this tree before PhaseManager.Awake.
        _phases = PhaseManager.Instance;
    }

    public void Initialize(TeamworkSettings teamwork)
    {
        _phases = PhaseManager.Instance;
        _node = GetComponent<TreeNode>();
        _workers = new WorkerSet(teamwork);

        if (_node != null && _node.Data != null)
        {
            _node.Data.OnStageDowngraded += HandleStageDowngraded;
        }
    }

    private void OnDestroy()
    {
        if (_node != null && _node.Data != null)
        {
            _node.Data.OnStageDowngraded -= HandleStageDowngraded;
        }
    }

    public Vector3 InteractionPoint => _node.InteractionPoint;

    public bool CanInteract =>
        _node != null &&
        _node.Data != null &&
        _node.IsTargetable &&
        _workers != null &&
        !_node.Data.IsFullyGrown &&
        !_awaitingNewCommand;

    /// <summary>
    /// Dipanggil saat pemain mengklik pohon untuk memulai sesi pertumbuhan fase berikutnya.
    /// </summary>
    public void PrepareForNewCommand()
    {
        _awaitingNewCommand = false;
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

        // Jika seluruh Spirit meninggalkan pohon sebelum fase selesai, reset progres ke 0%
        if (_workers.Count == 0)
        {
            _awaitingNewCommand = false;
            _progress = 0f;
        }

        UpdateProgressVfx();
    }

    public void Interact(SpiritController spirit, float deltaTime)
    {
        if (!CanInteract)
        {
            if (_node != null) _node.SetGrowthVfxActive(false);
            return;
        }

        UpdateProgressVfx();
        _progress += _workers.GetShare(deltaTime);

        if (_progress < GetCurrentDuration()) return;

        _progress = 0f;
        _node.Data.SetStage(_node.Data.Stage + 1);

        // Hentikan sesi saat ini agar Spirit kembali Idle dan menunggu klik untuk fase selanjutnya
        if (_stopAfterEachStage && !_node.Data.IsFullyGrown)
        {
            _awaitingNewCommand = true;
        }

        UpdateProgressVfx();
    }

    private void HandleStageDowngraded()
    {
        _progress = 0f;
        _awaitingNewCommand = false;
        UpdateProgressVfx();
    }

    private void UpdateProgressVfx()
    {
        if (_node != null)
            _node.SetGrowthVfxActive(IsWorking);
    }

    private float GetCurrentDuration()
    {
        float baseDuration = _node.Data.Definition.GetPhaseDuration(_node.Data.Stage);
        TreeRegistry registry = TreeRegistry.Instance;
        float multiplier = registry != null
            ? registry.GetGrowthTimeMultiplier(_node.Data.Element)
            : 1f;
        float bonus = _phases != null && _phases.Buffs != null ? _phases.Buffs.GetTotal(BuffType.TreeGrowthSpeed) : 0f;
        return baseDuration * multiplier / (1f + bonus);
    }

    private void OnDisable()
    {
        _progress = 0f;
        _awaitingNewCommand = false;
        if (_node != null) _node.SetGrowthVfxActive(false);
    }
}