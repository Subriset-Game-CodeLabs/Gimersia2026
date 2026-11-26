using System;
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
        public bool Unlocked = true;
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

    private int _selectedIndex;
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
                bool unlocked = _levels != null && i < _levels.Length && _levels[i].Unlocked;
                node.Configure(i + 1, unlocked, selected: false);
                node.Button.onClick.AddListener(() => SelectLevel(index, playSound: true));
                BindButtonHover(node.Button);
            }
        }
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
            if (_levels[i] != null && _levels[i].Unlocked && ShopRuntime.SelectedStage != null
                && _levels[i].Id == ShopRuntime.SelectedStage.Id)
            { SelectLevel(i, playSound: false); return; }
        for (int i = 0; i < _levels.Length; i++)
        {
            if (_levels[i].Unlocked)
            {
                SelectLevel(i, playSound: false);
                return;
            }
        }
        SelectLevel(0, playSound: false);
    }

    public void SelectLevel(int index)
    {
        SelectLevel(index, playSound: false);
    }

    private void SelectLevel(int index, bool playSound)
    {
        if (_levels == null || index < 0 || index >= _levels.Length) return;
        if (!_levels[index].Unlocked) return;

        if (playSound && _selectedIndex != index)
            AudioManager.Instance.PlayUiLevelNodeSelect();

        _selectedIndex = index;
        LevelEntry entry = _levels[index];

        if (_nodes != null)
        {
            for (int i = 0; i < _nodes.Length; i++)
                if (_nodes[i] != null) _nodes[i].SetSelectedNode(i == index);
        }

        if (_levelBannerText != null) _levelBannerText.text = entry.BannerLabel;
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
            _startButton.interactable = entry.Unlocked && !string.IsNullOrEmpty(entry.SceneName);
            if (_startButton.TryGetComponent(out UIStateButton stateBtn)) stateBtn.Refresh();
        }
    }

    private void StartSelectedLevel()
    {
        if (SceneFlow.IsLoading) return;
        if (_levels == null || _selectedIndex < 0 || _selectedIndex >= _levels.Length) return;
        LevelEntry entry = _levels[_selectedIndex];
        if (!entry.Unlocked || string.IsNullOrEmpty(entry.SceneName)) return;
        for (int i = 0; i < _levels.Length; i++)
            if (i != _selectedIndex && _levels[i] != null && _levels[i].Id == entry.Id)
            { Debug.LogError("Level IDs must be unique and stable.", this); return; }
        if (!ShopRuntime.TrySelectStage(entry.Id, entry.SceneName, entry.WinCoins, out string error))
        { Debug.LogError(error, this); return; }

        AudioManager.Instance.PlayUiClick();
        SceneFlow.Load(SceneFlow.PreparationShopScene);
    }
}