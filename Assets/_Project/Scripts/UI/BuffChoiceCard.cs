using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuffChoiceCard : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private Image _icon;
    private Action<int> _choose;
    private int _index;
    public bool IsConfigured => _button != null && _button.enabled && _button.gameObject.activeSelf
        && _title != null && _description != null;
    public Button Button => _button;

    private void OnEnable() { if (_button != null) _button.onClick.AddListener(Choose); }
    private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Choose); }

    public void Bind(BuffDefinition definition, int index, Action<int> choose, int currentStacks = 0)
    {
        _index = index;
        _choose = choose;
        _title.text = definition.Title;
        _description.text = $"{definition.Description}\nStacks: {currentStacks} → {currentStacks + 1}";
        if (_icon != null)
        {
            _icon.sprite = definition.Icon;
            _icon.enabled = definition.Icon != null;
        }
        _button.interactable = true;
    }

    public void Clear()
    {
        _choose = null;
        if (_button != null) _button.interactable = false;
    }

    private void Choose() { _choose?.Invoke(_index); }
}
