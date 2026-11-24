using UnityEngine;
using UnityEngine.UI;

public class EnemyHPBar : MonoBehaviour
{
    [Header("Referensi UI (Opsional - Otomatis Dibuat Jika Kosong)")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _delayFillImage;

    [Header("Ukuran & Posisi")]
    [SerializeField] private float _heightOffset = 2.15f;
    [SerializeField] private Vector2 _barSize = new Vector2(120f, 14f);
    [SerializeField] private float _worldScale = 0.01f;

    [Header("Warna & Animasi Delayed Drain")]
    [SerializeField] private Color _backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.78f);
    [SerializeField] private Color _hpRedColor = new Color(0.90f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color _delayDrainColor = new Color(1f, 0.85f, 0.75f, 0.95f);
    [SerializeField] private float _delayBeforeDrain = 0.28f;
    [SerializeField] private float _delayDrainSpeed = 0.75f;

    private EnemyController _enemy;
    private Camera _camera;
    private float _maxHp = 1f;
    private float _targetFill = 1f;
    private float _delayFill = 1f;
    private float _delayWaitTimer;
    private bool _built;

    public void Bind(EnemyController enemy, float maxHp)
    {
        _enemy = enemy;
        _maxHp = Mathf.Max(1f, maxHp);
        _targetFill = 1f;
        _delayFill = 1f;
        _delayWaitTimer = 0f;

        EnsureBarBuilt();
        if (_canvas != null) _canvas.gameObject.SetActive(true);
        if (_fillImage != null) _fillImage.fillAmount = 1f;
        if (_delayFillImage != null) _delayFillImage.fillAmount = 1f;
    }

    public void SetHp(float currentHp)
    {
        float nextFill = Mathf.Clamp01(currentHp / Mathf.Max(1f, _maxHp));
        if (nextFill < _targetFill)
        {
            _delayWaitTimer = _delayBeforeDrain;
        }
        else if (nextFill > _delayFill)
        {
            _delayFill = nextFill;
        }

        _targetFill = nextFill;
        if (_fillImage != null)
            _fillImage.fillAmount = _targetFill;
    }
    public void Show()
    {
        EnsureBarBuilt();
        if (_canvas != null)
            _canvas.gameObject.SetActive(true);
    }
    
    public void Hide()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_canvas == null || !_canvas.gameObject.activeSelf) return;

        if (_camera == null) _camera = Camera.main;
        if (_camera != null)
        {
            _canvas.transform.rotation = _camera.transform.rotation;
        }

        if (_delayFill > _targetFill)
        {
            if (_delayWaitTimer > 0f)
            {
                _delayWaitTimer -= Time.deltaTime;
            }
            else
            {
                _delayFill = Mathf.MoveTowards(_delayFill, _targetFill, _delayDrainSpeed * Time.deltaTime);
                if (_delayFillImage != null)
                    _delayFillImage.fillAmount = _delayFill;
            }
        }
        else if (_delayFill < _targetFill)
        {
            _delayFill = _targetFill;
            if (_delayFillImage != null)
                _delayFillImage.fillAmount = _delayFill;
        }
    }

    private void EnsureBarBuilt()
    {
        if (_built && _fillImage != null) return;
        _built = true;

        if (_canvas != null && _fillImage != null) return;

        // Buat World Space Canvas persegi panjang sederhana secara otomatis
        GameObject canvasObj = new GameObject("EnemyHPBarCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, _heightOffset, 0f);
        canvasObj.transform.localScale = Vector3.one * _worldScale;

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = _barSize;

        // Background gelap
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = _backgroundColor;
        bgImg.raycastTarget = false;
        StretchFull(bgObj.GetComponent<RectTransform>(), Vector2.zero);

        // Bar putih/terang di belakang yang berkurang perlahan (Delayed Drain)
        GameObject delayObj = new GameObject("DelayFill");
        delayObj.transform.SetParent(bgObj.transform, false);
        _delayFillImage = delayObj.AddComponent<Image>();
        _delayFillImage.sprite = CreateWhiteSprite();
        _delayFillImage.type = Image.Type.Filled;
        _delayFillImage.fillMethod = Image.FillMethod.Horizontal;
        _delayFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        _delayFillImage.color = _delayDrainColor;
        _delayFillImage.raycastTarget = false;
        StretchFull(delayObj.GetComponent<RectTransform>(), new Vector2(2f, 2f));

        // Bar HP utama berwarna merah
        GameObject fillObj = new GameObject("RedFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        _fillImage = fillObj.AddComponent<Image>();
        _fillImage.sprite = CreateWhiteSprite();
        _fillImage.type = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Horizontal;
        _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fillImage.color = _hpRedColor;
        _fillImage.raycastTarget = false;
        StretchFull(fillObj.GetComponent<RectTransform>(), new Vector2(2f, 2f));
    }

    private static void StretchFull(RectTransform rect, Vector2 padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
    }

    private static Sprite _sharedWhiteSprite;
    private static Sprite CreateWhiteSprite()
    {
        if (_sharedWhiteSprite != null) return _sharedWhiteSprite;
        Texture2D tex = Texture2D.whiteTexture;
        _sharedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return _sharedWhiteSprite;
    }
}