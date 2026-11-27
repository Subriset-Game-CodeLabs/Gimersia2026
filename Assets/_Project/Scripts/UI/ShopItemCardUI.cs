using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopItemCardUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _price;
    [SerializeField] private TMP_Text _owned;
    [SerializeField] private GameObject _selectedHighlight;
    private Action _select;
    public Button Button => _button;
    public bool IsConfigured => _button != null && _price != null && _owned != null
        && _selectedHighlight != null && _selectedHighlight != gameObject
        && _selectedHighlight.transform.IsChildOf(transform);

    private void OnEnable() { if (_button != null) _button.onClick.AddListener(Choose); }
    private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Choose); }
    private void Choose() => _select?.Invoke();

    public void Bind(PreparationShopConfig.Entry entry, int owned, bool selected, bool available, Action select)
    {
        _select = select;
        if (_title != null) _title.text = entry.Item.Title;
        if (_price != null) _price.text = entry.Price.ToString();
        if (_owned != null) _owned.text = owned.ToString();
        if (_icon != null) { _icon.sprite = entry.Item.Icon; _icon.enabled = entry.Item.Icon != null; }
        if (_selectedHighlight != null) _selectedHighlight.SetActive(selected);
        if (_button != null) _button.interactable = available;
    }

    public void Clear()
    {
        _select = null;
        if (_button != null) _button.interactable = false;
    }
}
