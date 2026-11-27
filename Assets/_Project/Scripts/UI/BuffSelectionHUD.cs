using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// Keep this component on an always-active Canvas host, not the hidden panel.
public sealed class BuffSelectionHUD : MonoBehaviour
{
    [SerializeField] private NightRewardController _controller;
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _confirmButton;
    private BuffDefinition _selectedDefinition;
    private bool _submitting;
    [SerializeField] private BuffChoiceCard[] _cards = new BuffChoiceCard[3];
    private readonly Tween[] _entrances = new Tween[3];
    private int _selectedIndex = -1;

    private void OnEnable()
    {
        if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
        if (_controller != null) _controller.OnChoicesChanged += Refresh;
        Refresh();
    }
    private void Start()
    {
        if (!IsConfiguredFor(_controller)) Debug.LogError("Configure BuffSelectionHUD controller, separate panel and three distinct cards/buttons.", this);
        Refresh();
    }
    private void OnDisable()
    {
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
        if (_controller != null)
        {
            _controller.OnChoicesChanged -= Refresh;
            if (_controller.IsSelecting) Debug.LogError("Reward HUD disabled during selection. Re-enable it to continue.", this);
        }
        Hide();
    }
    private void Refresh()
    {
        if (_controller == null || !_controller.IsSelecting)
        {
            Hide();
            return;
        }
        if (!IsConfiguredFor(_controller))
        {
            Debug.LogError("Reward UI is not configured; selection cannot be skipped.", this);
            return;
        }
        Show();
    }

    private void Show()
    {
        bool opening = !_panel.activeSelf;
        ResetSelection();
        _panel.SetActive(true);
        // Bind immediately: an entrance must never defer a reward transaction.
        for (int i = 0; i < _cards.Length; i++)
        {
            bool shown = i < _controller.Choices.Count;
            _cards[i].gameObject.SetActive(shown);
            if (shown) _cards[i].Bind(_controller.Choices[i], i, Choose);
            else _cards[i].Clear();
        }
        if (!opening) return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_cards[0].Button.gameObject);
        PlayEntrance();
    }

    private void Choose(int index)
    {
        if (_submitting || !isActiveAndEnabled || _panel == null || !_panel.activeInHierarchy
            || _controller == null || !_controller.IsSelecting || index < 0 || index >= _controller.Choices.Count
            || _cards == null || index >= _cards.Length || _cards[index] == null
            || !_cards[index].isActiveAndEnabled || _cards[index].Button == null
            || !_cards[index].Button.isActiveAndEnabled || !_cards[index].Button.IsInteractable()) return;
        if (_selectedIndex == index && _selectedDefinition == _controller.Choices[index]) return;
        _selectedIndex = index;
        _selectedDefinition = _controller.Choices[index];
        for (int i = 0; i < _cards.Length; i++) _cards[i].SetSelected(i == _selectedIndex);
        if (_confirmButton != null) _confirmButton.interactable = true;
        // Only accepted choices sound; refresh/reset and repeated clicks stay silent.
        AudioManager audio = AudioManager.Instance;
        if (audio != null) audio.PlayUiClick();
    }

    private void ResetSelection()
    {
        _selectedIndex = -1;
        _selectedDefinition = null;
        if (_confirmButton != null) _confirmButton.interactable = false;
    }

    private void Confirm()
    {
        if (_submitting || !isActiveAndEnabled || _confirmButton == null || !_confirmButton.isActiveAndEnabled
            || !_confirmButton.interactable || _panel == null || !_panel.activeInHierarchy
            || _controller == null || !_controller.IsSelecting || !IsConfiguredFor(_controller)) return;
        int index = _selectedIndex;
        if (index < 0 || index >= _controller.Choices.Count || _controller.Choices[index] != _selectedDefinition)
        {
            ResetSelection();
            return;
        }
        _submitting = true;
        _confirmButton.interactable = false;
        try
        {
            if (!_controller.TryChoose(index)) Show();
        }
        finally { _submitting = false; }
    }

    private void Hide()
    {
        ResetSelection();
        ResetEntrance();
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected != null && _panel != null && selected.transform.IsChildOf(_panel.transform))
            events.SetSelectedGameObject(null);

        if (HasSeparatePanel()) _panel.SetActive(false);
        if (_cards != null)
        {
            foreach (BuffChoiceCard card in _cards)
            {
                if (card == null) continue;
                card.Clear();
                if (_panel != null && card.transform.IsChildOf(_panel.transform)) card.gameObject.SetActive(true);
            }
        }
    }

    private void PlayEntrance()
    {
        ResetEntrance();
        for (int i = 0; i < _cards.Length; i++)
        {
            Transform card = _cards[i].transform;
            card.localScale = Vector3.one * 0.7f;
            _entrances[i] = DOTween.To(() => card.localScale.x,
                value => card.localScale = Vector3.one * value, 1f, 0.5f)
                .SetDelay(i * 0.045f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    private void ResetEntrance()
    {
        for (int i = 0; i < _entrances.Length; i++)
        {
            _entrances[i]?.Kill();
            _entrances[i] = null;
            if (_cards != null && i < _cards.Length && _cards[i] != null)
                _cards[i].transform.localScale = Vector3.one;
        }
    }

    public bool IsConfiguredFor(NightRewardController controller)
    {
        if (!isActiveAndEnabled || _controller != controller) return false;
        if (!HasSeparatePanel()) return false;
        if (_confirmButton == null || !_confirmButton.enabled || !_confirmButton.gameObject.activeSelf
            || !_confirmButton.transform.IsChildOf(_panel.transform)) return false;
        if (_cards != null)
            foreach (BuffChoiceCard card in _cards)
                if (card != null && card.Button == _confirmButton) return false;
        return HasConfiguredCards();
    }

    // Hiding the panel must not disable this component's event subscription.
    private bool HasSeparatePanel()
    {
        if (_panel == null || _panel == gameObject) return false;
        return !transform.IsChildOf(_panel.transform);
    }

    private bool HasConfiguredCards()
    {
        if (_cards == null || _cards.Length != 3) return false;

        for (int i = 0; i < _cards.Length; i++)
        {
            BuffChoiceCard card = _cards[i];
            if (card != null && _controller != null && _controller.IsSelecting && i >= _controller.Choices.Count && !card.gameObject.activeSelf) continue;
            if (card == null || !card.enabled || !card.gameObject.activeSelf) return false;
            if (!card.IsConfigured || !card.transform.IsChildOf(_panel.transform)) return false;

            for (int j = 0; j < i; j++)
            {
                if (card == _cards[j] || card.Button == _cards[j].Button) return false;
            }
        }
        return true;
    }
}
