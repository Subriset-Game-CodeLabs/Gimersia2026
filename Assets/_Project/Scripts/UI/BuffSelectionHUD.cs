using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

// Keep this component on an always-active Canvas host, not the hidden panel.
public sealed class BuffSelectionHUD : MonoBehaviour
{
    [SerializeField] private NightRewardController _controller;
    [SerializeField] private GameObject _panel;
    [SerializeField] private BuffChoiceCard[] _cards = new BuffChoiceCard[3];
    private readonly Tween[] _entrances = new Tween[3];

    private void OnEnable()
    {
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
        _panel.SetActive(true);
        // Bind immediately: an entrance must never defer a reward transaction.
        for (int i = 0; i < _cards.Length; i++)
        {
            BuffDefinition definition = _controller.Choices[i];
            _cards[i].Bind(definition, i, Choose, _controller.State.GetStacks(definition.Type));
        }
        if (!opening) return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_cards[0].Button.gameObject);
        PlayEntrance();
    }

    private void Choose(int index)
    {
        if (_controller != null) _controller.TryChoose(index);
    }

    private void Hide()
    {
        ResetEntrance();
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected != null && _panel != null && selected.transform.IsChildOf(_panel.transform))
            events.SetSelectedGameObject(null);

        if (_cards != null)
        {
            foreach (BuffChoiceCard card in _cards)
            {
                if (card != null) card.Clear();
            }
        }
        if (HasSeparatePanel()) _panel.SetActive(false);
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
