using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TreeElementMenu : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _powerText;
    [SerializeField] private Button _waterButton;
    [SerializeField] private Button _fireButton;
    [SerializeField] private Button _lightningButton;
    [SerializeField] private Button _healButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private SpiritCommander _commander;
    [SerializeField] private SpiritSelection _selection;

    private TreeNode _tree;
    private TreeRegistry _registry;
    private PhaseManager _phases;
    private string _feedback = "";
    private void Awake()
    {
        _waterButton.onClick.AddListener(ChooseWater);
        _fireButton.onClick.AddListener(ChooseFire);
        _lightningButton.onClick.AddListener(ChooseLightning);
        _healButton.onClick.AddListener(Heal);
        _closeButton.onClick.AddListener(Close);
        _panel.gameObject.SetActive(false);
    }
    private void Start()
    {
        _phases = PhaseManager.Instance;
        if (_phases != null) _phases.OnRewardStart += HandleRewardStart;
        _registry = TreeRegistry.Instance;
        if (_registry != null)
            _registry.OnPowerChanged += Refresh;
    }
    private void OnDestroy()
    {
        if (_phases != null) _phases.OnRewardStart -= HandleRewardStart;
        if (_registry != null)
            _registry.OnPowerChanged -= Refresh;
        if (_tree != null)
        {
            _tree.OnAvailabilityChanged -= HandleTreeAvailabilityChanged;
            _tree.Data.OnHpChanged -= Refresh;
        }
        _waterButton.onClick.RemoveListener(ChooseWater);
        _fireButton.onClick.RemoveListener(ChooseFire);
        _lightningButton.onClick.RemoveListener(ChooseLightning);
        _healButton.onClick.RemoveListener(Heal);
        _closeButton.onClick.RemoveListener(Close);
    }
    public void Open(TreeNode tree, Vector2 screenPosition)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) return;
        if (tree == null || !tree.IsTargetable ||
            !tree.Data.IsFullyGrown)
            return;
        Close();
        _tree = tree;
        _tree.OnAvailabilityChanged += HandleTreeAvailabilityChanged;
        _tree.Data.OnHpChanged += Refresh;
        _feedback = "";
        RectTransform canvasRect = (RectTransform)_canvas.transform;
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
        {
            Vector2 half = _panel.rect.size * 0.5f;
            Rect bounds = canvasRect.rect;
            _panel.anchoredPosition = new Vector2(
                Mathf.Clamp(localPoint.x, bounds.xMin + half.x, bounds.xMax - half.x),
                Mathf.Clamp(localPoint.y, bounds.yMin + half.y, bounds.yMax - half.y));
        }
        _panel.gameObject.SetActive(true);
        Refresh();
    }
    public void Close()
    {
        if (_tree != null)
        {
            _tree.OnAvailabilityChanged -= HandleTreeAvailabilityChanged;
            _tree.Data.OnHpChanged -= Refresh;
        }
        _tree = null;
        _panel.gameObject.SetActive(false);
    }
    private void ChooseWater() => Choose(ElementType.Water);
    private void ChooseFire() => Choose(ElementType.Fire);
    private void ChooseLightning() => Choose(ElementType.Lightning);
    private void Choose(ElementType element)
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) { Close(); return; }
        if (_tree == null || _registry == null ||
            _registry.Settings == null)
            return;
        if (_tree.Data.Element == element)
        {
            _feedback = "Tree already has that element";
            Refresh();
            return;
        }
        if (_tree.Data.Element == ElementType.None)
        {
            if (_tree.Data.TryAssignElement(element, _registry.Settings))
                _feedback = $"Assigned {element}";
            else
                _feedback = "Cannot assign element";
            Refresh();
            return;
        }
        if (_selection == null || _selection.Selected.Count == 0)
        {
            _feedback = "Select a spirit to reassign this tree";
            Refresh();
            return;
        }
        TreeElementInteractable work =
            _tree.GetComponent<TreeElementInteractable>();
        if (work == null || !work.Queue(element))
        {
            _feedback = "Cannot reassign element";
            Refresh();
            return;
        }
        _commander.InteractSelected(work);
        Close();
    }
    private void Heal()
    {
        if (PhaseManager.Instance != null && !PhaseManager.Instance.GameplayInputAllowed) { Close(); return; }
        if (_tree == null) return;
        if (_selection == null || _selection.Selected.Count == 0)
        {
            _feedback = "Select a spirit to heal this tree";
            Refresh();
            return;
        }

        TreeHealInteractable work = _tree.GetComponent<TreeHealInteractable>();
        if (work == null || !work.CanInteract)
        {
            _feedback = "Tree does not need healing";
            Refresh();
            return;
        }
        _commander.InteractSelected(work);
        Close();
    }
    private void HandleRewardStart(int night) => Close();
    private void HandleTreeAvailabilityChanged(TreeNode tree)
    {
        if (tree == null || !tree.IsTargetable)
            Close();
        else
            Refresh();
    }
    private void Refresh()
    {
        if (_tree == null || !_panel.gameObject.activeSelf) return;
        _statusText.text =
            $"Tree: {_tree.Data.Element}\n" +
            $"HP: {_tree.Data.Hp:0}/{_tree.Data.Definition.MaxHp:0}\n" +
            _feedback;
        _healButton.interactable =
            _tree.Data.NeedsHealing &&
            _selection != null && _selection.Selected.Count > 0;
        if (_registry == null) return;
        _powerText.text =
            $"Water: {_registry.GetTotalPower(ElementType.Water)}\n" +
            $"Fire: {_registry.GetTotalPower(ElementType.Fire)}\n" +
            $"Lightning: {_registry.GetTotalPower(ElementType.Lightning)}";
    }
}
