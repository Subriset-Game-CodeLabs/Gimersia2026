using TMPro;
using UnityEngine;

public class TreeCountHUD : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private string _numberFormat = "{0}";

    [Header("Filter Fase")]
    [Tooltip("Centang agar pohon yang naik ke Complete tetap terhitung, dan hanya berkurang jika turun kembali ke Initial.")]
    [SerializeField] private bool _includeCompleteStage = true;

    private TreeRegistry _subscribedRegistry;
    private int _lastCount = -1;

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

        if (currentCount == _lastCount) return;

        _lastCount = currentCount;
        _countText.text = string.Format(_numberFormat, currentCount);
    }
}