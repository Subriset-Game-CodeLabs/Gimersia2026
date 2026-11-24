using System;
using UnityEngine;

public class TreeNode : MonoBehaviour
{
    [Header("3D Visual Models per Stage & Element")]
    [Tooltip("Model 3D saat fase Initial (Tunas awal / Pohon tumbang awal).")]
    [SerializeField] private GameObject _initialVisual;
    [Tooltip("Model 3D saat fase Intermediate (Tumbuh setengah).")]
    [SerializeField] private GameObject _intermediateVisual;
    [Tooltip("Model 3D saat fase Complete sebelum dipilih elemennya.")]
    [SerializeField] private GameObject _completeNeutralVisual;

    [Header("3D Visual Models Setelah Memilih Elemen")]
    [SerializeField] private GameObject _fireTreeVisual;
    [SerializeField] private GameObject _waterTreeVisual;
    [SerializeField] private GameObject _lightningTreeVisual;

    [Header("UI Position per Stage & Element (Bar HP & Progression)")]
    [Tooltip("Otomatis terisi TreeWorldCanvas jika dikosongkan.")]
    [SerializeField] private Transform _worldUiRoot;

    [Tooltip("Atur tinggi Y (local) posisi Bar HP & Progression untuk masing-masing model 3D pohon.")]
    [SerializeField] private float _initialUiHeight = 1.1f;
    [SerializeField] private float _intermediateUiHeight = 1.8f;
    [SerializeField] private float _completeNeutralUiHeight = 2.5f;
    [SerializeField] private float _fireUiHeight = 2.7f;
    [SerializeField] private float _waterUiHeight = 2.7f;
    [SerializeField] private float _lightningUiHeight = 2.7f;

    [Header("Opsional: Transform Anchor Khusus per Model 3D")]
    [Tooltip("Jika diisi, posisi UI akan mengikuti posisi Transform ini (menggantikan Height Offset di atas).")]
    [SerializeField] private Transform _initialUiAnchor;
    [SerializeField] private Transform _intermediateUiAnchor;
    [SerializeField] private Transform _completeNeutralUiAnchor;
    [SerializeField] private Transform _fireUiAnchor;
    [SerializeField] private Transform _waterUiAnchor;
    [SerializeField] private Transform _lightningUiAnchor;

    [Header("VFX Progression (Aktif Saat Dikerjakan Spirit)")]
    [Tooltip("VFX yang muncul selama Spirit sedang menumbuhkan/merestorasi pohon.")]
    [SerializeField] private GameObject _growthProgressVfx;
    [Tooltip("VFX yang muncul selama Spirit sedang mengganti elemen pohon.")]
    [SerializeField] private GameObject _elementChangeProgressVfx;

    [Header("VFX One-Shot Perpindahan Fase")]
    [Tooltip("Prefab VFX ledakan/kilau saat pohon naik fase atau berubah menjadi pohon elemen.")]
    [SerializeField] private GameObject _stageUpVfxPrefab;
    [Tooltip("Prefab VFX saat pohon turun fase akibat serangan musuh.")]
    [SerializeField] private GameObject _stageDownVfxPrefab;
    [SerializeField] private Transform _vfxSpawnPoint;

    [Header("Polish & Feedback")]
    [SerializeField] private TreeVisualFeedback _visualFeedback;
    private TreeUnderAttackIndicator _underAttackIndicator;

    public TreeData Data { get; private set; }
    public event Action<TreeNode> OnAvailabilityChanged;

    public bool IsRevealed { get; private set; }
    // Spirit dapat berinteraksi pada semua fase (Initial, Intermediate, Complete)
    public bool IsTargetable => IsRevealed && Data != null && Data.IsAlive && gameObject.activeInHierarchy;
    // Musuh HANYA menyerang pohon dari fase Intermediate dan Complete
    public bool IsEnemyTargetable => IsTargetable && Data.IsVulnerableToEnemies;

    public Vector3 TargetPoint => transform.position;
    private Transform _interactionPoint;
    public Vector3 InteractionPoint => _interactionPoint != null ? _interactionPoint.position : transform.position;

    private void Awake()
    {
        if (_visualFeedback == null)
            _visualFeedback = GetComponent<TreeVisualFeedback>();

        if (_worldUiRoot == null)
        {
            Canvas worldCanvas = GetComponentInChildren<Canvas>(true);
            if (worldCanvas != null)
                _worldUiRoot = worldCanvas.transform;
        }

        _underAttackIndicator = GetComponent<TreeUnderAttackIndicator>();
        if (_underAttackIndicator == null)
            _underAttackIndicator = gameObject.AddComponent<TreeUnderAttackIndicator>();

        SetGrowthVfxActive(false);
        SetElementChangeVfxActive(false);
    }

    public void SetInteractionPoint(Transform point) => _interactionPoint = point;

    public void Initialize(TreeDefinition definition)
    {
        Data = new TreeData(definition);
        Data.OnStageChanged += Refresh;
        Data.OnElementChanged += Refresh;
        Data.OnStageUpgraded += HandleStageUpgraded;
        Data.OnStageDowngraded += HandleStageDowngraded;
        Data.OnDamaged += HandleDamaged;

        Refresh();
        SetRevealed(false);
    }

    public void SetRevealed(bool revealed)
    {
        IsRevealed = revealed;
        Refresh();
    }

    private void OnDestroy()
    {
        if (Data != null)
        {
            Data.OnStageChanged -= Refresh;
            Data.OnElementChanged -= Refresh;
            Data.OnStageUpgraded -= HandleStageUpgraded;
            Data.OnStageDowngraded -= HandleStageDowngraded;
            Data.OnDamaged -= HandleDamaged;
        }
    }

    private void Refresh()
    {
        if (Data == null) return;

        GameObject targetVisual = ResolveActiveVisual();

        SetVisualState(_initialVisual, targetVisual == _initialVisual);
        SetVisualState(_intermediateVisual, targetVisual == _intermediateVisual);
        SetVisualState(_completeNeutralVisual, targetVisual == _completeNeutralVisual);
        SetVisualState(_fireTreeVisual, targetVisual == _fireTreeVisual);
        SetVisualState(_waterTreeVisual, targetVisual == _waterTreeVisual);
        SetVisualState(_lightningTreeVisual, targetVisual == _lightningTreeVisual);

        UpdateWorldUiPosition();

        Transform visualRoot = targetVisual != null ? targetVisual.transform : null;
        if (_visualFeedback != null)
            _visualFeedback.SetActiveVisualRoot(visualRoot);
        if (_underAttackIndicator != null)
            _underAttackIndicator.SetActiveVisualRoot(visualRoot);

        gameObject.SetActive(IsRevealed && Data.IsAlive);
        OnAvailabilityChanged?.Invoke(this);
    }

    private void UpdateWorldUiPosition()
    {
        if (_worldUiRoot == null || Data == null) return;

        Transform targetAnchor = null;
        float targetHeight = _initialUiHeight;

        switch (Data.Stage)
        {
            case TreeStage.Initial:
                targetAnchor = _initialUiAnchor;
                targetHeight = _initialUiHeight;
                break;

            case TreeStage.Intermediate:
                targetAnchor = _intermediateUiAnchor;
                targetHeight = _intermediateUiHeight;
                break;

            case TreeStage.Complete:
                if (Data.Element == ElementType.Fire)
                {
                    targetAnchor = _fireUiAnchor;
                    targetHeight = _fireUiHeight;
                }
                else if (Data.Element == ElementType.Water)
                {
                    targetAnchor = _waterUiAnchor;
                    targetHeight = _waterUiHeight;
                }
                else if (Data.Element == ElementType.Lightning)
                {
                    targetAnchor = _lightningUiAnchor;
                    targetHeight = _lightningUiHeight;
                }
                else
                {
                    targetAnchor = _completeNeutralUiAnchor;
                    targetHeight = _completeNeutralUiHeight;
                }
                break;
        }

        if (targetAnchor != null)
        {
            _worldUiRoot.position = targetAnchor.position;
        }
        else
        {
            _worldUiRoot.localPosition = new Vector3(0f, targetHeight, 0f);
        }
    }

    private GameObject ResolveActiveVisual()
    {
        switch (Data.Stage)
        {
            case TreeStage.Initial:
                return _initialVisual;

            case TreeStage.Intermediate:
                return _intermediateVisual != null ? _intermediateVisual : _initialVisual;

            case TreeStage.Complete:
                if (Data.Element == ElementType.Fire && _fireTreeVisual != null)
                    return _fireTreeVisual;
                if (Data.Element == ElementType.Water && _waterTreeVisual != null)
                    return _waterTreeVisual;
                if (Data.Element == ElementType.Lightning && _lightningTreeVisual != null)
                    return _lightningTreeVisual;
                return _completeNeutralVisual != null ? _completeNeutralVisual : _intermediateVisual;

            default:
                return _initialVisual;
        }
    }

    private static void SetVisualState(GameObject obj, bool active)
    {
        if (obj != null && obj.activeSelf != active)
            obj.SetActive(active);
    }

    public void SetGrowthVfxActive(bool active)
    {
        if (_growthProgressVfx != null && _growthProgressVfx.activeSelf != active)
            _growthProgressVfx.SetActive(active);
    }

    public void SetElementChangeVfxActive(bool active)
    {
        if (_elementChangeProgressVfx != null && _elementChangeProgressVfx.activeSelf != active)
            _elementChangeProgressVfx.SetActive(active);
    }

    public void PlayClickFeedback()
    {
        if (_visualFeedback != null)
            _visualFeedback.PlayClickBounce();
    }

    private void HandleStageUpgraded()
    {
        SpawnTransitionVfx(_stageUpVfxPrefab);
        if (_visualFeedback != null)
            _visualFeedback.PlayStageUpTransition();
    }

    private void HandleStageDowngraded()
    {
        SpawnTransitionVfx(_stageDownVfxPrefab);
        if (_visualFeedback != null)
            _visualFeedback.PlayStageDownTransition();
    }

    private void HandleDamaged(float amount, Vector3 hitDirection)
    {
        if (_visualFeedback != null)
            _visualFeedback.PlayEnemyHitRecoil(hitDirection);
    }

    private void SpawnTransitionVfx(GameObject prefab)
    {
        if (prefab == null || !gameObject.activeInHierarchy) return;
        Vector3 pos = _vfxSpawnPoint != null ? _vfxSpawnPoint.position : transform.position;
        Instantiate(prefab, pos, Quaternion.identity);
    }
}