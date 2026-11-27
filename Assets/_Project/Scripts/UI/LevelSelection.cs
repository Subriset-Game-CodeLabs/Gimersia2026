using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Level Selection screen (05 - Level Selection.pdf):
// Left board displays the selected level preview, name, description, and info tags + START button below.
// Right board shows the 12-node map path; clicking any unlocked node updates the info board.
public class LevelSelection : MonoBehaviour
{
    [Serializable]
    public class LevelEntry
    {
        public string BannerLabel = "LEVEL 1";
        public string LevelTitle = "HUTAN PINUS TUA";
        [TextArea(2, 4)] public string Description =
            "Bersihkan 5 grid yang tercemar, tumbuhkan pohon elemen, lalu lindungi hutan dari 7 gelombang monster bersama 3 Forest Spirit.";
        public Sprite PreviewSprite;
        public string[] Tags = { "7 Malam", "± 17 Menit", "5 Grid", "3 Spirit" };
        public string SceneName = SceneFlow.GameplayScene;
        public string Id;
        // TODO(balance): configure a fixed reward per stage; zero until designed.
        [Min(0)] public int WinCoins;
        [Tooltip("Starting-access override. First entry is available automatically; later entries unlock after the preceding entry is completed. Leave false for normal progression.")]
        public bool Unlocked;
    }

    [Header("Navigation")]
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _startButton;

    [Header("Left Info Board")]
    [SerializeField] private TMP_Text _levelBannerText;
    [SerializeField] private Image _previewImage;
    [SerializeField] private TMP_Text _levelTitleText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text[] _tagLabels;

    [Header("Right Map List")]
    [SerializeField] private UILevelNodeButton[] _nodes;
    [SerializeField] private LevelEntry[] _levels;

    private int _selectedIndex = -1;
    private HashSet<string> _completedLevelIds;
    private bool[] _validLevels;
    private UIFocusOnNavigate _focusNavigator;

    private void Awake()
    {
        if (!TryGetComponent(out _focusNavigator))
            _focusNavigator = gameObject.AddComponent<UIFocusOnNavigate>();

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(HandleBackClicked);
            BindButtonHover(_backButton);
        }

        if (_startButton != null)
        {
            _startButton.onClick.AddListener(StartSelectedLevel);
            BindButtonHover(_startButton);
        }

        if (_nodes != null)
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                int index = i;
                UILevelNodeButton node = _nodes[i];
                if (node == null) continue;
                node.Button.onClick.AddListener(() => SelectLevel(index, playSound: true));
                BindButtonHover(node.Button);
            }
        }
    }

    private void OnEnable()
    {
        RefreshProgression();
        SelectFirstUnlocked();
    }

    private void RefreshProgression()
    {
        _selectedIndex = -1;
        if (!ShopRuntime.TryLoadProgression(out _completedLevelIds, out string error))
            Debug.LogError("Level progression unavailable: " + error, this);
        else if (error != null) Debug.LogWarning(error);

        _validLevels = new bool[_levels != null ? _levels.Length : 0];
        if (_validLevels.Length == 0) Debug.LogError("Configure the ordered level list.", this);
        for (int i = 0; i < _validLevels.Length; i++)
        {
            LevelEntry entry = _levels[i];
            bool valid = entry != null && !string.IsNullOrWhiteSpace(entry.Id) && entry.Id.Length <= 256
                && !string.IsNullOrWhiteSpace(entry.SceneName) && entry.WinCoins >= 0
                && Application.CanStreamedLevelBeLoaded(entry.SceneName);
            if (valid)
                for (int j = 0; j < _levels.Length; j++)
                    if (i != j && _levels[j] != null && _levels[j].Id == entry.Id) { valid = false; break; }
            _validLevels[i] = valid;
            if (!valid) Debug.LogError($"Level entry {i + 1} requires a unique stable ID (1–256 characters), a gameplay scene in the Build Profile and nonnegative WinCoins.", this);
        }
        if (_nodes != null)
            for (int i = 0; i < _nodes.Length; i++)
                if (_nodes[i] != null)
                    _nodes[i].Configure(i + 1, IsLevelAvailable(i), selected: false, completed: IsLevelCompleted(i));
        if (_startButton != null) _startButton.interactable = false;
    }

    private bool IsLevelCompleted(int index) => _completedLevelIds != null && _validLevels != null
        && index >= 0 && index < _validLevels.Length && _validLevels[index]
        && _completedLevelIds.Contains(_levels[index].Id);

    private bool IsLevelAvailable(int index)
    {
        if (_completedLevelIds == null || _validLevels == null || index < 0
            || index >= _validLevels.Length || !_validLevels[index]) return false;
        return index == 0 || _levels[index].Unlocked || IsLevelCompleted(index) || IsLevelCompleted(index - 1);
    }

    private void BindButtonHover(Button button)
    {
        if (button == null) return;
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            if (button.interactable)
                AudioManager.Instance.PlayUiHover();
        });
        trigger.triggers.Add(entry);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        AudioManager.Instance.PlayMenuMusic();

        RefreshProgression();
        SelectFirstUnlocked();
        // Start with no button pre-hovered; first Arrow press focuses START.
        _focusNavigator.SetDefaultTarget(_startButton, clearCurrentSelection: true);
    }

    private void OnDestroy()
    {
        if (_backButton != null) _backButton.onClick.RemoveListener(HandleBackClicked);
        if (_startButton != null) _startButton.onClick.RemoveListener(StartSelectedLevel);
        if (_nodes != null)
        {
            for (int i = 0; i < _nodes.Length; i++)
                if (_nodes[i] != null) _nodes[i].Button.onClick.RemoveAllListeners();
        }
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            HandleBackClicked();
    }

    private void HandleBackClicked()
    {
        if (SceneFlow.IsLoading) return;
        AudioManager.Instance.PlayUiClick();
        SceneFlow.LoadMainMenu();
    }

    private void SelectFirstUnlocked()
    {
        if (_levels == null || _levels.Length == 0) return;
        for (int i = 0; i < _levels.Length; i++)
            if (IsLevelAvailable(i) && ShopRuntime.SelectedStage != null
                && _levels[i].Id == ShopRuntime.SelectedStage.Id)
            { SelectLevel(i, playSound: false); return; }
        for (int i = 0; i < _levels.Length; i++)
        {
            if (IsLevelAvailable(i))
            {
                SelectLevel(i, playSound: false);
                return;
            }
        }
    }

    public void SelectLevel(int index)
    {
        SelectLevel(index, playSound: false);
    }

    private void SelectLevel(int index, bool playSound)
    {
        if (!IsLevelAvailable(index)) return;

        if (playSound && _selectedIndex != index)
            AudioManager.Instance.PlayUiLevelNodeSelect();

        _selectedIndex = index;
        LevelEntry entry = _levels[index];

        if (_nodes != null)
        {
            for (int i = 0; i < _nodes.Length; i++)
                if (_nodes[i] != null) _nodes[i].SetSelectedNode(i == index);
        }

        if (_levelBannerText != null)
            _levelBannerText.text = string.IsNullOrWhiteSpace(entry.BannerLabel) ? $"LEVEL {index + 1}" : entry.BannerLabel;
        if (_levelTitleText != null) _levelTitleText.text = entry.LevelTitle;
        if (_descriptionText != null) _descriptionText.text = entry.Description;
        if (_previewImage != null && entry.PreviewSprite != null) _previewImage.sprite = entry.PreviewSprite;

        if (_tagLabels != null)
        {
            for (int i = 0; i < _tagLabels.Length; i++)
            {
                TMP_Text label = _tagLabels[i];
                if (label == null) continue;
                bool hasTag = entry.Tags != null && i < entry.Tags.Length && !string.IsNullOrEmpty(entry.Tags[i]);
                if (label.transform.parent != null) label.transform.parent.gameObject.SetActive(hasTag);
                if (hasTag) label.text = entry.Tags[i];
            }
        }

        if (_startButton != null)
        {
            _startButton.interactable = IsLevelAvailable(index);
            if (_startButton.TryGetComponent(out UIStateButton stateBtn)) stateBtn.Refresh();
        }
    }

    private void StartSelectedLevel()
    {
        if (SceneFlow.IsLoading) return;
        if (!IsLevelAvailable(_selectedIndex)) return;
        LevelEntry entry = _levels[_selectedIndex];
        if (!ShopRuntime.TrySelectStage(entry.Id, entry.SceneName, entry.WinCoins, out string error))
        { Debug.LogError(error, this); return; }

        AudioManager.Instance.PlayUiClick();
        SceneFlow.Load(SceneFlow.PreparationShopScene);
    }
}