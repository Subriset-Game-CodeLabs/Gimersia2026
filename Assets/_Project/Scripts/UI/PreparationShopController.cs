using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keep this controller on an active object OUTSIDE the shop panels.
public sealed class PreparationShopController : MonoBehaviour
{
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
    [Tooltip("Optional status/error presentation. Errors also go to the Console.")]
    [SerializeField] private TMP_Text _status;
    private ShopProgress _progress;
    private int _selected;
    private bool _ready;

    private void Awake()
    {
        _ready = ValidateControls(out string error) && ShopRuntime.TryInitialize(_config, out error);
        if (!_ready) { ShowError(error); return; }
        _progress = ShopRuntime.Progress;
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

    private void Start() => Refresh();

    private void OnDisable()
    {
        if (_backButton != null) _backButton.onClick.RemoveListener(Back);
        if (_startButton != null) _startButton.onClick.RemoveListener(StartLevel);
        if (_buyButton != null) _buyButton.onClick.RemoveListener(Buy);
        if (_progress != null) _progress.OnChanged -= Refresh;
        SceneFlow.OnLoadingChanged -= Refresh;
        if (_cards != null) foreach (ShopItemCardUI card in _cards) if (card != null) card.Clear();
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
        _selected = index;
        if (_status != null) _status.text = string.Empty;
        Refresh();
    }

    private void Buy()
    {
        if (!_ready || SceneFlow.IsLoading) return;
        PreparationShopConfig.Entry entry = _config.Entries[_selected];
        if (!_progress.TryBuy(entry.Item.Id, entry.Price, out string error)) ShowError(error);
        else if (_status != null) _status.text = "Purchased 1 " + entry.Item.Title + ".";
        Refresh();
    }

    private void Back() { if (!SceneFlow.IsLoading) SceneFlow.LoadLevelSelection(); }
    private void StartLevel()
    {
        if (!_ready || SceneFlow.IsLoading) return;
        SelectedStage stage = ShopRuntime.SelectedStage;
        if (stage == null) { ShowError("Select a level before starting gameplay."); return; }
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
        if (_backButton != null) _backButton.interactable = !SceneFlow.IsLoading;
        if (_startButton != null) _startButton.interactable = available && ShopRuntime.SelectedStage != null;
        if (_buyButton != null) _buyButton.interactable = false;
        if (!_ready) return;
        _coins.text = _progress.Coins.ToString();
        for (int i = 0; i < _cards.Length; i++)
        {
            int index = i;
            PreparationShopConfig.Entry entry = _config.Entries[i];
            _cards[i].Bind(entry, _progress.Inventory.GetQuantity(entry.Item.Id), i == _selected, available, () => Select(index));
        }
        PreparationShopConfig.Entry selected = _config.Entries[_selected];
        if (_detailTitle != null) _detailTitle.text = selected.Item.Title;
        if (_detailIcon != null) { _detailIcon.sprite = selected.Item.Icon; _detailIcon.enabled = selected.Item.Icon != null; }
        _detailDescription.text = selected.Item.Description;
        _detailOwned.text = _progress.Inventory.GetQuantity(selected.Item.Id).ToString();
        _detailPrice.text = selected.Price.ToString();
        _buyButton.interactable = available && _progress.CanBuy(selected.Item.Id, selected.Price);
    }
}
