using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TreeCountHUD : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private string _numberFormat = "{0}";

    [Header("Visual Feedback (UI v2)")]
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Sprite _normalBackgroundSprite;
    [SerializeField] private Sprite _dangerBackgroundSprite;
    [SerializeField] private RectTransform _punchTarget;
    [SerializeField, Min(0)] private int _dangerThreshold = 2;

    [Header("Filter Fase")]
    [Tooltip("Centang agar pohon yang naik ke Complete tetap terhitung, dan hanya berkurang jika turun kembali ke Initial.")]
    [SerializeField] private bool _includeCompleteStage = true;

    private TreeRegistry _subscribedRegistry;
    private int _lastCount = -1;
    private bool _hasHadTrees;
    private Tween _punchTween;

    private void Awake()
    {
        if (_countText == null)
            _countText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void Start()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        _punchTween?.Kill();
        if (_subscribedRegistry != null)
        {
            _subscribedRegistry.OnCountChanged -= Refresh;
            _subscribedRegistry = null;
        }
    }

    private void Update()
    {
        if (_subscribedRegistry == null && TreeRegistry.Instance != null)
        {
            TrySubscribe();
            Refresh();
        }
    }

    private void TrySubscribe()
    {
        TreeRegistry registry = TreeRegistry.Instance;
        if (registry == null || _subscribedRegistry == registry) return;

        if (_subscribedRegistry != null)
            _subscribedRegistry.OnCountChanged -= Refresh;

        _subscribedRegistry = registry;
        _subscribedRegistry.OnCountChanged += Refresh;
    }

    public void Refresh()
    {
        if (_countText == null) return;

        int currentCount = TreeRegistry.Instance != null
            ? TreeRegistry.Instance.GetGrownTreeCount(_includeCompleteStage)
            : 0;

        if (currentCount > 0) _hasHadTrees = true;

        bool changed = _lastCount >= 0 && currentCount != _lastCount;
        if (currentCount == _lastCount) return;

        _lastCount = currentCount;
        _countText.text = string.Format(_numberFormat, currentCount);

        if (_backgroundImage != null)
        {
            bool danger = _hasHadTrees && currentCount <= _dangerThreshold;
            Sprite chosen = danger && _dangerBackgroundSprite != null
                ? _dangerBackgroundSprite
                : _normalBackgroundSprite;
            if (chosen != null) _backgroundImage.sprite = chosen;
        }

        if (changed)
        {
            Transform target = _punchTarget != null ? _punchTarget : _countText.transform;
            _punchTween?.Kill();
            target.localScale = Vector3.one;
            _punchTween = target.DOPunchScale(Vector3.one * 0.14f, 0.24f, 5, 0.5f)
                .SetUpdate(true)
                .SetLink(target.gameObject);
        }
    }
}
