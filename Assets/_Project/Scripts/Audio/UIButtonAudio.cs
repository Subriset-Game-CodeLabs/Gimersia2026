using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class UIButtonAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
{
    public enum ClickSoundType
    {
        StandardClick,
        StartGame,
        LevelNodeSelect,
        None
    }

    [SerializeField] private bool _playHoverSound = true;
    [SerializeField] private ClickSoundType _clickSound = ClickSoundType.StandardClick;

    private Selectable _selectable;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
    }

    public void Configure(ClickSoundType clickSound, bool playHover = true)
    {
        _clickSound = clickSound;
        _playHoverSound = playHover;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanPlay()) return;
        if (_playHoverSound) AudioManager.Instance.PlayUiHover();
    }

    public void OnSelect(BaseEventData eventData)
    {
        // Bunyi hover juga terdengar saat navigasi keyboard (Arrow keys)
        if (!CanPlay() || eventData is PointerEventData) return;
        if (_playHoverSound) AudioManager.Instance.PlayUiHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanPlay() || eventData.button != PointerEventData.InputButton.Left) return;
        TriggerClickSound();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!CanPlay()) return;
        TriggerClickSound();
    }

    private bool CanPlay()
    {
        return _selectable != null && _selectable.IsActive() && _selectable.IsInteractable();
    }

    private void TriggerClickSound()
    {
        switch (_clickSound)
        {
            case ClickSoundType.StandardClick:
                AudioManager.Instance.PlayUiClick();
                break;
            case ClickSoundType.StartGame:
                AudioManager.Instance.PlayUiStartGame();
                break;
            case ClickSoundType.LevelNodeSelect:
                AudioManager.Instance.PlayUiLevelNodeSelect();
                break;
        }
    }

    public static void Attach(Selectable target, ClickSoundType clickSound = ClickSoundType.StandardClick, bool playHover = true)
    {
        if (target == null) return;
        if (!target.TryGetComponent(out UIButtonAudio audio))
            audio = target.gameObject.AddComponent<UIButtonAudio>();
        audio.Configure(clickSound, playHover);
    }
}