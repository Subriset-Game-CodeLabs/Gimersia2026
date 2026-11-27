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

    [Header("Random Grid Rotation")]
    [Tooltip("Mengacak rotasi sumbu Y pohon kelipatan 90 derajat (0, 90, 180, 270) saat spawn agar selaras dengan grid pulau.")]
    [SerializeField] private bool _randomizeYRotation90 = true;

    [Header("Elemental Pool of Light (Pencahayaan Malam & Siang)")]
    [Tooltip("Opsional: Point Light anak pada prefab pohon. Otomatis dibuat jika dikosongkan.")]
    [SerializeField] private Light _elementPointLight;
    [SerializeField] private float _lightHeightOffset = 1.65f;
    [SerializeField, Min(0.5f)] private float _elementLightRange = 4.8f;
    [SerializeField, Min(0f)] private float _nightLightIntensity = 2.6f;
    [SerializeField, Min(0f)] private float _dayLightIntensity = 0.25f;
    [SerializeField, Min(0.05f)] private float _lightFadeDuration = 1.8f;
    [SerializeField] private Color _fireLightColor = new Color(1f, 0.48f, 0.16f, 1f);
    [SerializeField] private Color _waterLightColor = new Color(0.22f, 0.85f, 1f, 1f);
    [SerializeField] private Color _lightningLightColor = new Color(1f, 0.84f, 0.24f, 1f);

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

    private PhaseManager _phases;
    private bool _isNight;
    private float _currentLightIntensity;
    private float _targetLightIntensity;
    private Color _currentLightColor = Color.white;
    private Color _targetLightColor = Color.white;
    private float _lightPulseSeed;
    private Quaternion _initialUiWorldRotation = Quaternion.identity;

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

        if (_worldUiRoot != null)
            _initialUiWorldRotation = _worldUiRoot.rotation;

        _underAttackIndicator = GetComponent<TreeUnderAttackIndicator>();
        if (_underAttackIndicator == null)
            _underAttackIndicator = gameObject.AddComponent<TreeUnderAttackIndicator>();

        _lightPulseSeed = Mathf.Abs(GetInstanceID() * 0.173f);
        EnsureElementLight();
        ApplyRandomGridRotation();

        SetGrowthVfxActive(false);
        SetElementChangeVfxActive(false);
    }

    private void OnEnable()
    {
        BindPhaseEvents();
        UpdateElementLightTarget(instant: true);
    }

    private void OnDisable()
    {
        UnbindPhaseEvents();
        if (Data != null) Data.ClearItemStatus();
    }

    public void SetInteractionPoint(Transform point) => _interactionPoint = point;

    public void Initialize(TreeDefinition definition)
    {
        if (Data != null) Data.ClearItemStatus();

        ApplyRandomGridRotation();

        Data = new TreeData(definition);
        Data.OnStageChanged += Refresh;
        Data.OnElementChanged += Refresh;
        Data.OnStageUpgraded += HandleStageUpgraded;
        Data.OnStageDowngraded += HandleStageDowngraded;
        Data.OnDamaged += HandleDamaged;

        Refresh();
        SetRevealed(false);
    }

    private void ApplyRandomGridRotation()
    {
        if (!_randomizeYRotation90) return;

        float randomY = UnityEngine.Random.Range(0, 4) * 90f;
        Vector3 euler = transform.localEulerAngles;
        transform.localEulerAngles = new Vector3(euler.x, randomY, euler.z);

        if (_worldUiRoot != null)
            _worldUiRoot.rotation = _initialUiWorldRotation;
    }

    public void SetRevealed(bool revealed)
    {
        IsRevealed = revealed;
        Refresh();
    }

    private void OnDestroy()
    {
        UnbindPhaseEvents();
        if (Data == null) return;

        Data.ClearItemStatus();
        Data.OnStageChanged -= Refresh;
        Data.OnElementChanged -= Refresh;
        Data.OnStageUpgraded -= HandleStageUpgraded;
        Data.OnStageDowngraded -= HandleStageDowngraded;
        Data.OnDamaged -= HandleDamaged;
    }

    private void BindPhaseEvents()
    {
        PhaseManager current = PhaseManager.Instance;
        if (_phases == current)
        {
            if (_phases != null)
                _isNight = _phases.CurrentPhase == PhaseType.Night;
            return;
        }

        UnbindPhaseEvents();
        _phases = current;
        if (_phases != null)
        {
            _isNight = _phases.CurrentPhase == PhaseType.Night;
            _phases.OnDayStart += HandleDayStart;
            _phases.OnNightStart += HandleNightStart;
        }
    }

    private void UnbindPhaseEvents()
    {
        if (_phases == null) return;
        _phases.OnDayStart -= HandleDayStart;
        _phases.OnNightStart -= HandleNightStart;
        _phases = null;
    }

    private void HandleDayStart(int day)
    {
        _isNight = false;
        UpdateElementLightTarget(instant: false);
    }

    private void HandleNightStart(int night)
    {
        _isNight = true;
        UpdateElementLightTarget(instant: false);
    }

    private void Update()
    {
        if (_elementPointLight == null) return;
        if (!_elementPointLight.enabled && _targetLightIntensity <= 0.001f) return;

        float step = (_lightFadeDuration > 0.01f)
            ? (Mathf.Max(_nightLightIntensity, _dayLightIntensity) / _lightFadeDuration) * Time.deltaTime
            : 100f;

        _currentLightIntensity = Mathf.MoveTowards(_currentLightIntensity, _targetLightIntensity, step);
        _currentLightColor = Color.Lerp(_currentLightColor, _targetLightColor, 8f * Time.deltaTime);

        if (_currentLightIntensity <= 0.001f && _targetLightIntensity <= 0.001f)
        {
            _currentLightIntensity = 0f;
            _elementPointLight.intensity = 0f;
            _elementPointLight.enabled = false;
            return;
        }

        _elementPointLight.enabled = true;
        _elementPointLight.color = _currentLightColor;

        float pulse = (_isNight && _currentLightIntensity > 0.1f)
            ? 1f + Mathf.Sin((Time.time * 2.2f) + _lightPulseSeed) * 0.06f
            : 1f;

        _elementPointLight.intensity = _currentLightIntensity * pulse;
    }

    private void EnsureElementLight()
    {
        if (_elementPointLight == null)
        {
            GameObject lightObj = new GameObject("ElementPoolLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, _lightHeightOffset, 0f);
            _elementPointLight = lightObj.AddComponent<Light>();
        }

        _elementPointLight.type = LightType.Point;
        _elementPointLight.shadows = LightShadows.None;
        _elementPointLight.range = _elementLightRange;
        _elementPointLight.intensity = 0f;
        _elementPointLight.enabled = false;
    }

    private void UpdateElementLightTarget(bool instant)
    {
        if (_elementPointLight == null) return;

        if (_phases != null)
            _isNight = _phases.CurrentPhase == PhaseType.Night;

        bool hasElementLight = IsRevealed && Data != null && Data.IsAlive
            && Data.Stage == TreeStage.Complete && Data.Element != ElementType.None;

        if (!hasElementLight)
        {
            _targetLightIntensity = 0f;
        }
        else
        {
            _targetLightIntensity = _isNight ? _nightLightIntensity : _dayLightIntensity;
            _targetLightColor = ResolveElementLightColor(Data.Element);
        }

        _elementPointLight.range = _elementLightRange;

        if (instant)
        {
            _currentLightIntensity = _targetLightIntensity;
            _currentLightColor = _targetLightColor;
            _elementPointLight.color = _currentLightColor;
            _elementPointLight.intensity = _currentLightIntensity;
            _elementPointLight.enabled = _currentLightIntensity > 0.001f;
        }
        else if (_targetLightIntensity > 0.001f)
        {
            if (_currentLightIntensity <= 0.001f)
                _currentLightColor = _targetLightColor;
            _elementPointLight.enabled = true;
        }
    }

    private Color ResolveElementLightColor(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return _fireLightColor;
            case ElementType.Water: return _waterLightColor;
            case ElementType.Lightning: return _lightningLightColor;
            default: return Color.white;
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
        UpdateElementLightTarget(instant: !gameObject.activeInHierarchy);

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

        if (_randomizeYRotation90)
            _worldUiRoot.rotation = _initialUiWorldRotation;
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
        AudioManager.Instance.PlayTreeClick(transform.position);
        if (_visualFeedback != null)
            _visualFeedback.PlayClickBounce();
    }

    private void HandleStageUpgraded()
    {
        SpawnTransitionVfx(_stageUpVfxPrefab);
        if (_visualFeedback != null)
            _visualFeedback.PlayStageUpTransition();

        if (Data != null && Data.Element != ElementType.None)
        {
            AudioManager.Instance.PlayTreeElementAssigned(Data.Element, transform.position);
        }
        else
        {
            AudioManager.Instance.PlayTreeStageUp(transform.position);
        }
    }

    private void HandleStageDowngraded()
    {
        SpawnTransitionVfx(_stageDownVfxPrefab);
        if (_visualFeedback != null)
            _visualFeedback.PlayStageDownTransition();

        AudioManager.Instance.PlayTreeStageDown(transform.position);
    }

    private void HandleDamaged(float amount, Vector3 hitDirection)
    {
        AudioManager.Instance.PlayTreeHitByEnemy(transform.position);
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