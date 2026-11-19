using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Styled Win / Lose pop-up board (09 - Win.pdf, 10 - Lose.pdf, Result.pdf)
// with live run stats (remaining trees, elapsed time, wave), Restart, and Pilih Level buttons.
public class ResultHud : MonoBehaviour
{
    // Optional: found in the scene when left empty, so the prefab drops into any level.
    [SerializeField] private WinLoseChecker _checker;
    [SerializeField] private GameObject _panel;

    [Header("Banner")]
    [SerializeField] private Image _bannerImage;
    [SerializeField] private Sprite _winBannerSprite;
    [SerializeField] private Sprite _loseBannerSprite;
    [SerializeField] private TMP_Text _bannerLabel;

    [Header("Mascot Groups")]
    [SerializeField] private GameObject _winMascots;
    [SerializeField] private GameObject _loseMascot;

    [Header("Copy")]
    [SerializeField] private TMP_Text _label;
    [SerializeField] private TMP_Text _subtitle;

    [Header("Stat Tags")]
    [SerializeField] private GameObject _tag1Root;
    [SerializeField] private TMP_Text _tag1Text;
    [SerializeField] private GameObject _tag2Root;
    [SerializeField] private TMP_Text _tag2Text;
    [SerializeField] private GameObject _tag3Root;
    [SerializeField] private TMP_Text _tag3Text;

    [Header("Buttons")]
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _levelSelectButton;

    private UIFocusOnNavigate _focusNavigator;

    private void Awake()
    {
        if (!TryGetComponent(out _focusNavigator))
            _focusNavigator = gameObject.AddComponent<UIFocusOnNavigate>();

        if (_panel != null) _panel.SetActive(false);

        if (_checker == null) _checker = FindAnyObjectByType<WinLoseChecker>();
        if (_checker == null)
        {
            Debug.LogError("ResultHud found no WinLoseChecker in the scene.", this);
            return;
        }
        _checker.OnResult += HandleResult;

        if (_restartButton != null) _restartButton.onClick.AddListener(SceneFlow.RestartCurrent);
        if (_levelSelectButton != null) _levelSelectButton.onClick.AddListener(SceneFlow.LoadLevelSelection);
    }

    private void OnDestroy()
    {
        if (_checker != null) _checker.OnResult -= HandleResult;
        if (_restartButton != null) _restartButton.onClick.RemoveListener(SceneFlow.RestartCurrent);
        if (_levelSelectButton != null) _levelSelectButton.onClick.RemoveListener(SceneFlow.LoadLevelSelection);
    }

    private void HandleResult(RunResult result)
    {
        bool won = result == RunResult.Won;
        PhaseManager phases = PhaseManager.Instance;
        TreeRegistry trees = TreeRegistry.Instance;

        int totalDays = phases != null ? phases.TotalDays : 7;
        int currentDay = phases != null ? Mathf.Max(1, phases.CurrentDay) : 1;
        int aliveTrees = trees != null ? trees.AliveCount : 0;

        if (_bannerImage != null)
            _bannerImage.sprite = won ? _winBannerSprite : _loseBannerSprite;
        if (_bannerLabel != null)
            _bannerLabel.text = won ? "MENANG!" : "KALAH...";

        if (_winMascots != null) _winMascots.SetActive(won);
        if (_loseMascot != null) _loseMascot.SetActive(!won);

        if (_label != null)
        {
            _label.text = won
                ? $"HUTAN BERHASIL BERTAHAN {totalDays} MALAM!"
                : "SEMUA POHON TELAH TUMBANG...";
        }

        if (_subtitle != null)
        {
            _subtitle.text = won
                ? "Kalian bekerja sama dengan hebat. Gotong royong menyelamatkan hutan!"
                : "Jangan menyerah! Coba ganti elemen pohon sesuai kelemahan musuh.";
        }

        if (won)
        {
            SetTag(_tag1Root, _tag1Text, $"Pohon tersisa: {aliveTrees}");
            SetTag(_tag2Root, _tag2Text, $"Waktu: {FormatDuration(Time.timeSinceLevelLoad)}");
            SetTag(_tag3Root, _tag3Text, $"Wave {totalDays}/{totalDays}");
        }
        else
        {
            SetTag(_tag1Root, _tag1Text, "Pohon tersisa: 0");
            SetTag(_tag2Root, _tag2Text, $"Bertahan sampai Wave {currentDay}");
            SetTag(_tag3Root, _tag3Text, null);
        }

        if (_panel != null) _panel.SetActive(true);

        // Keep buttons clean until mouse hover or first Arrow key press.
        _focusNavigator.SetDefaultTarget(_restartButton, clearCurrentSelection: true);
    }

    private static void SetTag(GameObject root, TMP_Text label, string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (root != null) root.SetActive(show);
        if (show && label != null) label.text = text;
    }

    private static string FormatDuration(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
