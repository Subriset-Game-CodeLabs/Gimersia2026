using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Keep this controller on an active object OUTSIDE the shop panels.
public sealed class PreparationShopController : MonoBehaviour
{
    private static readonly Color TagRed = new Color(0.84f, 0.16f, 0.16f, 1f);
    private static readonly Color TagBlue = new Color(0.11f, 0.51f, 0.85f, 1f);
    private static readonly Color TagGreen = new Color(0.18f, 0.54f, 0.22f, 1f);
    private static readonly Color TagBrown = new Color(0.55f, 0.38f, 0.22f, 1f);

    [SerializeField] private PreparationShopConfig _config;
    [SerializeField] private ShopItemCardUI[] _cards = Array.Empty<ShopItemCardUI>();
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TMP_Text _coins;
    [SerializeField] private TMP_Text _detailTitle;
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TMP_Text _detailDescription;
    [SerializeField] private TMP_Text _detailOwned;
    [SerializeField] private TMP_Text _detailPrice;
    [SerializeField] private TMP_Text _detailTag1;
    [SerializeField] private TMP_Text _detailTag2;
    [Tooltip("Optional status/error presentation. Errors also go to the Console.")]
    [SerializeField] private TMP_Text _status;

    private ShopProgress _progress;
    private UIFocusOnNavigate _focusNavigator;
    private Tween _coinsPunch;
    private Tween _ownedPunch;
    private int _selected;
    private bool _ready;

    private void Awake()
    {
        if (!TryGetComponent(out _focusNavigator))
            _focusNavigator = gameObject.AddComponent<UIFocusOnNavigate>();

        _ready = ValidateControls(out string error) && ShopRuntime.TryInitialize(_config, out error);
        if (!_ready) { ShowError(error); return; }
        _progress = ShopRuntime.Progress;

        BindButtonHover(_backButton);
        BindButtonHover(_startButton);
        BindButtonHover(_buyButton);
    }

    private void OnEnable()
    {
        if (_backButton != null) _backButton.onClick.AddListener(Back);
        if (_startButton != null) _startButton.onClick.AddListener(StartLevel);
        if (_buyButton != null) _buyButton.onClick.AddListener(Buy);
        if (_progress != null) _progress.OnChanged += Refresh;
        SceneFlow.OnLoadingChanged += Refresh;
        Refresh();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        AudioManager.Instance.PlayMenuMusic();
        Refresh();
        _focusNavigator.SetDefaultTarget(_startButton, clearCurrentSelection: true);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            Back();
    }

    private void OnDisable()
    {
        if (_backButton != null) _backButton.onClick.RemoveListener(Back);
        if (_startButton != null) _startButton.onClick.RemoveListener(StartLevel);
        if (_buyButton != null) _buyButton.onClick.RemoveListener(Buy);
        if (_progress != null) _progress.OnChanged -= Refresh;
        SceneFlow.OnLoadingChanged -= Refresh;
        if (_cards != null) foreach (ShopItemCardUI card in _cards) if (card != null) card.Clear();
        KillFeedbackTweens();
    }

    private void OnDestroy() => KillFeedbackTweens();

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

    private bool ValidateControls(out string error)
    {
        error = null;
        if (_config == null || !_config.TryValidate(out error))
        { if (error == null) error = "Assign a valid shop config."; return false; }
        if (_backButton == null || _startButton == null || _buyButton == null || _coins == null
            || _detailOwned == null || _detailPrice == null || _detailDescription == null
            || _backButton == _startButton || _buyButton == _backButton || _buyButton == _startButton
            || _cards == null || _cards.Length != _config.Entries.Count)
        { error = "Assign distinct Back/Start/Buy buttons, coin/detail text and one card per shop entry."; return false; }
        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i] == null || !_cards[i].IsConfigured || !_cards[i].enabled
                || !_cards[i].gameObject.activeSelf || !_cards[i].Button.transform.IsChildOf(_cards[i].transform)
                || _cards[i].Button == _backButton || _cards[i].Button == _startButton || _cards[i].Button == _buyButton)
            { error = "Each card needs its own button, price, owned text and child highlight."; return false; }
            for (int j = 0; j < i; j++)
                if (_cards[j] == _cards[i] || _cards[j].Button == _cards[i].Button)
                { error = "Shop cards must have distinct buttons and components."; return false; }
            // Disabling a card/panel must not disable its controlling scene listener.
            if (transform.IsChildOf(_cards[i].transform))
            { error = "Keep the scene controller outside item cards and panels."; return false; }
        }
        return true;
    }

    private void Select(int index)
    {
        if (!_ready || SceneFlow.IsLoading) return;
        if (_selected != index)
            AudioManager.Instance.PlayUiLevelNodeSelect();
        _selected = index;
        if (_status != null) _status.text = string.Empty;
        Refresh();
    }

    private void Buy()
    {
        if (!_ready || SceneFlow.IsLoading) return;
        PreparationShopConfig.Entry entry = _config.Entries[_selected];
        if (!_progress.TryBuy(entry.Item.Id, entry.Price, out string error))
        {
            AudioManager.Instance.PlayUiActionBlocked();
            ShowError(error);
        }
        else
        {
            AudioManager.Instance.PlayUiClick();
            if (_status != null) _status.text = "Purchased 1 " + entry.Item.Title + ".";
            PunchText(_coins, ref _coinsPunch);
            PunchText(_detailOwned, ref _ownedPunch);
        }
        Refresh();
    }

    private void Back()
    {
        if (SceneFlow.IsLoading) return;
        AudioManager.Instance.PlayUiClick();
        SceneFlow.LoadLevelSelection();
    }

    private void StartLevel()
    {
        if (!_ready || SceneFlow.IsLoading) return;
        SelectedStage stage = ShopRuntime.SelectedStage;
        if (stage == null)
        {
            AudioManager.Instance.PlayUiActionBlocked();
            ShowError("Select a level before starting gameplay.");
            return;
        }
        AudioManager.Instance.PlayUiStartGame();
        SceneFlow.Load(stage.SceneName);
    }

    private void ShowError(string error)
    {
        if (_status != null) _status.text = error;
        Debug.LogError("Preparation shop: " + error, this);
    }

    private void Refresh()
    {
        bool available = _ready && !SceneFlow.IsLoading;
        SetButtonInteractable(_backButton, !SceneFlow.IsLoading);
        SetButtonInteractable(_startButton, available && ShopRuntime.SelectedStage != null);
        SetButtonInteractable(_buyButton, false);
        if (!_ready) return;

        _coins.text = _progress.Coins.ToString();
        for (int i = 0; i < _cards.Length; i++)
        {
            int index = i;
            PreparationShopConfig.Entry entry = _config.Entries[i];
            _cards[i].Bind(entry, _progress.Inventory.GetQuantity(entry.Item.Id), i == _selected, available, () => Select(index));
        }

        PreparationShopConfig.Entry selected = _config.Entries[_selected];
        ItemDefinition item = selected.Item;
        int owned = _progress.Inventory.GetQuantity(item.Id);

        if (_detailTitle != null) _detailTitle.text = item.Title.ToUpperInvariant();
        if (_detailIcon != null) { _detailIcon.sprite = item.Icon; _detailIcon.enabled = item.Icon != null; }
        _detailDescription.text = item.Description;
        _detailOwned.text = $"OWNED: {owned}";
        _detailPrice.text = selected.Price.ToString();
        UpdateDetailTags(item);
        SetButtonInteractable(_buyButton, available && _progress.CanBuy(item.Id, selected.Price));
    }

    private void UpdateDetailTags(ItemDefinition item)
    {
        if (_detailTag1 == null && _detailTag2 == null) return;

        string tag1 = string.Empty;
        Color color1 = TagBrown;
        string tag2 = string.Empty;
        Color color2 = TagBrown;

        int seconds = Mathf.Max(1, Mathf.RoundToInt(item.Duration));
        switch (item.Effect)
        {
            case ItemEffectType.AreaDamage:
                tag1 = "AREA DAMAGE"; color1 = TagRed;
                tag2 = "ONE USE"; color2 = TagGreen;
                break;
            case ItemEffectType.AreaFreeze:
                tag1 = "AREA FREEZE"; color1 = TagBlue;
                tag2 = $"{seconds} SECONDS"; color2 = TagBrown;
                break;
            case ItemEffectType.InstantGrowth:
                tag1 = "INSTANT GROW"; color1 = TagGreen;
                tag2 = "1 TREE"; color2 = TagBrown;
                break;
            case ItemEffectType.TreeInvulnerability:
                tag1 = "AREA SHIELD"; color1 = TagGreen;
                tag2 = $"{seconds} SECONDS"; color2 = TagBrown;
                break;
            case ItemEffectType.TeamHaste:
                tag1 = "+SPIRIT SPEED"; color1 = TagBlue;
                tag2 = $"{seconds} SECONDS"; color2 = TagBrown;
                break;
            case ItemEffectType.TeamEmpowerment:
                tag1 = "DAMAGE X POWER"; color1 = TagRed;
                tag2 = "3 SPIRITS"; color2 = TagBrown;
                break;
        }

        if (_detailTag1 != null)
        {
            _detailTag1.text = tag1;
            _detailTag1.color = color1;
        }
        if (_detailTag2 != null)
        {
            _detailTag2.text = tag2;
            _detailTag2.color = color2;
        }
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button == null) return;
        button.interactable = interactable;
        if (button.TryGetComponent(out UIStateButton stateBtn))
            stateBtn.Refresh();
    }

    private void PunchText(TMP_Text target, ref Tween slot)
    {
        if (target == null || !Application.isPlaying || !gameObject.activeInHierarchy) return;
        if (slot != null && slot.IsActive()) slot.Kill();
        RectTransform rect = target.rectTransform;
        rect.localScale = Vector3.one;
        slot = rect.DOPunchScale(new Vector3(0.18f, 0.18f, 0f), 0.22f, 6, 0.6f)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void KillFeedbackTweens()
    {
        if (_coinsPunch != null && _coinsPunch.IsActive()) _coinsPunch.Kill();
        _coinsPunch = null;
        if (_coins != null) _coins.rectTransform.localScale = Vector3.one;

        if (_ownedPunch != null && _ownedPunch.IsActive()) _ownedPunch.Kill();
        _ownedPunch = null;
        if (_detailOwned != null) _detailOwned.rectTransform.localScale = Vector3.one;
    }
}
