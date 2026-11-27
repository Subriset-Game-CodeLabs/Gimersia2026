using UnityEngine;
using UnityEngine.UI;

public class TreeHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TreeNode _treeNode;
    [SerializeField] private GameObject _barRoot;
    [SerializeField] private Image _currentHpFill;
    [SerializeField] private Image _delayedWhiteFill;

    [Header("Smooth Drain Settings")]
    [Tooltip("Kecepatan bar utama turun mengikuti HP terbaru.")]
    [SerializeField] private float _mainBarLerpSpeed = 14f;
    [Tooltip("Jeda waktu (detik) bar putih bertahan sebelum mulai menyusut.")]
    [SerializeField] private float _whiteChipDelay = 0.25f;
    [Tooltip("Kecepatan bar putih menyusut menyusul bar utama.")]
    [SerializeField] private float _whiteChipLerpSpeed = 4.5f;

    [Header("Visibility")]
    [SerializeField] private bool _faceCamera = true;

    private Camera _mainCamera;
    private float _targetRatio = 1f;
    private float _displayedMainRatio = 1f;
    private float _displayedWhiteRatio = 1f;
    private float _chipDelayTimer;
    private bool _hasBeenAttacked;
    private bool _subscribed;

    private void Awake()
    {
        if (_treeNode == null) _treeNode = GetComponentInParent<TreeNode>();
        _mainCamera = Camera.main;
        SetVisible(false);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed || _treeNode == null || _treeNode.Data == null) return;

        _treeNode.Data.OnDamaged += HandleDamaged;
        _treeNode.Data.OnHpChanged += HandleHpChanged;
        _treeNode.Data.OnStageChanged += HandleStageOrElementReset;
        _treeNode.Data.OnElementChanged += HandleStageOrElementReset;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _treeNode == null || _treeNode.Data == null) return;

        _treeNode.Data.OnDamaged -= HandleDamaged;
        _treeNode.Data.OnHpChanged -= HandleHpChanged;
        _treeNode.Data.OnStageChanged -= HandleStageOrElementReset;
        _treeNode.Data.OnElementChanged -= HandleStageOrElementReset;
        _subscribed = false;
    }

    private void HandleDamaged(float damageAmount, Vector3 hitDirection)
    {
        if (_treeNode == null || _treeNode.Data == null || !_treeNode.Data.IsVulnerableToEnemies)
            return;

        _hasBeenAttacked = true;
        _chipDelayTimer = _whiteChipDelay;
        UpdateTargetRatio();
        SetVisible(true);
    }

    private void HandleHpChanged()
    {
        if (_treeNode == null || _treeNode.Data == null) return;

        UpdateTargetRatio();

        // Jika HP sudah disembuhkan penuh oleh Spirit, sembunyikan bar HP
        if (!_treeNode.Data.IsVulnerableToEnemies || _targetRatio >= 0.999f)
        {
            _hasBeenAttacked = false;
            _displayedMainRatio = 1f;
            _displayedWhiteRatio = 1f;
            SetVisible(false);
        }
    }

    private void HandleStageOrElementReset()
    {
        // Saat berpindah tier baru (100 HP penuh) atau turun ke Initial, reset bar
        _hasBeenAttacked = false;
        _targetRatio = 1f;
        _displayedMainRatio = 1f;
        _displayedWhiteRatio = 1f;
        SetVisible(false);
    }

    private void UpdateTargetRatio()
    {
        TreeData data = _treeNode.Data;
        _targetRatio = data.MaxHp > 0f ? Mathf.Clamp01(data.Hp / data.MaxHp) : 0f;

        // Jika HP bertambah (disembuhkan), bar putih langsung ikut naik
        if (_targetRatio > _displayedWhiteRatio)
            _displayedWhiteRatio = _targetRatio;
    }

    private void LateUpdate()
    {
        if (!_hasBeenAttacked || _barRoot == null || !_barRoot.activeSelf) return;

        // 1. Update bar utama
        _displayedMainRatio = Mathf.MoveTowards(
            _displayedMainRatio,
            _targetRatio,
            _mainBarLerpSpeed * Time.deltaTime
        );

        // 2. Tahan bar putih sebentar lalu turunkan secara perlahan
        if (_displayedWhiteRatio > _displayedMainRatio)
        {
            if (_chipDelayTimer > 0f)
            {
                _chipDelayTimer -= Time.deltaTime;
            }
            else
            {
                _displayedWhiteRatio = Mathf.MoveTowards(
                    _displayedWhiteRatio,
                    _displayedMainRatio,
                    _whiteChipLerpSpeed * Time.deltaTime
                );
            }
        }
        else
        {
            _displayedWhiteRatio = _displayedMainRatio;
        }

        if (_currentHpFill != null) _currentHpFill.fillAmount = _displayedMainRatio;
        if (_delayedWhiteFill != null) _delayedWhiteFill.fillAmount = _displayedWhiteRatio;

        if (_faceCamera)
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera != null)
                transform.rotation = _mainCamera.transform.rotation;
        }
    }

    private void SetVisible(bool visible)
    {
        if (_barRoot != null && _barRoot.activeSelf != visible)
            _barRoot.SetActive(visible);
    }
}