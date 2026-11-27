using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BagItemSlotUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [Tooltip("Optional label for expanded layouts; leave empty for icon-only slots.")]
    [SerializeField] private TMP_Text _title;
    [Tooltip("Optional label for expanded layouts; leave empty for icon-only slots.")]
    [SerializeField] private TMP_Text _description;
    [SerializeField] private TMP_Text _quantity;
    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _selectedIndicator;
    private Action<string> _choose;
    private string _id;
    private bool _available;

    public Button Button => _button;
    public bool IsConfigured => _button != null && _button.enabled && _button.gameObject.activeSelf
        && _quantity != null;

    private void OnEnable() { if (_button != null) _button.onClick.AddListener(Choose); }
    private void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(Choose);
    }

    public void Bind(ItemDefinition item, int quantity, bool selected, Action<string> choose)
    {
        if (!IsConfigured || item == null) { Clear(); return; }
        _id = item.Id;
        _choose = choose;
        _available = quantity > 0;
        if (_title != null) _title.text = item.Title;
        if (_description != null) _description.text = item.Description;
        _quantity.text = quantity.ToString();
        _button.interactable = _available;
        if (_icon != null)
        {
            _icon.sprite = item.Icon;
            _icon.enabled = item.Icon != null;
        }
        if (_selectedIndicator != null) _selectedIndicator.SetActive(selected);
    }

    public void Clear()
    {
        _id = null;
        _choose = null;
        _available = false;
        if (_button != null) _button.interactable = false;
        if (_selectedIndicator != null) _selectedIndicator.SetActive(false);
    }

    private void Choose()
    {
        if (isActiveAndEnabled && _available) _choose?.Invoke(_id);
    }
}
