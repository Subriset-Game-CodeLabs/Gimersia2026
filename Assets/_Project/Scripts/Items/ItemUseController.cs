using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(BagHUD))]
public sealed class ItemUseController : MonoBehaviour
{
    [SerializeField] private ItemTargetResolver _targetResolver;
    [SerializeField] private ItemTargetPreview _preview;
    [Tooltip("Visual marker size for a single tree; not an effect radius.")]
    [SerializeField, Min(0.01f)] private float _singleTreePreviewRadius = 0.6f;

    [Header("UI v2 Aim Tooltip & Target Pin")]
    [SerializeField] private Canvas _hudCanvas;
    [SerializeField] private RectTransform _aimTooltipRoot;
    [SerializeField] private Image _aimTooltipIcon;
    [SerializeField] private TMP_Text _aimTooltipText;
    [SerializeField] private Vector2 _aimTooltipOffset = new Vector2(185f, 82f);

    [SerializeField] private RectTransform _targetPinRoot;
    [SerializeField] private Image _targetPinImage;
    [SerializeField] private Image _targetPinIcon;
    [SerializeField] private Sprite _targetPinValidSprite;
    [SerializeField] private Sprite _targetPinInvalidSprite;

    [Header("UI v2 Item Used Impact Burst")]
    [SerializeField] private RectTransform _impactBurstRoot;
    [SerializeField] private TMP_Text _impactBurstText;
    [SerializeField] private CanvasGroup _impactBurstGroup;

    private BagHUD _bag;
    private PhaseManager _phases;
    private ItemUseSession _session;
    private InputAction _confirm;
    private InputAction _cancel;
    private bool _confirmPending;
    private bool _usingTeam;
    private bool _teamCancelled;
    private Vector2 _pendingPosition;
    private Tween _impactTween;

    public bool IsTargeting => _session != null && _session.IsTargeting;

    private bool IsConfigured => _bag != null && _bag.InventoryProvider != null
        && _targetResolver != null && _targetResolver.IsConfigured
        && _preview != null && _preview.IsConfigured;

    private void Awake()
    {
        _bag = GetComponent<BagHUD>();
        if (_hudCanvas == null) _hudCanvas = GetComponentInParent<Canvas>();

        _confirm = new InputAction("ConfirmItem", InputActionType.Button);
        _confirm.AddBinding("<Mouse>/leftButton");
        _cancel = new InputAction("CancelItem", InputActionType.Button);
        _cancel.AddBinding("<Keyboard>/escape");
        _cancel.AddBinding("<Mouse>/rightButton");

        HideAimFeedback();
        if (_impactBurstRoot != null) _impactBurstRoot.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (_bag != null) _bag.OnItemSelected += HandleItemSelected;
        _confirm.performed += QueueConfirmation;
        _cancel.performed += HandleCancel;
        _confirm.Enable();
        _cancel.Enable();
        BindPhases();
    }

    private void Start()
    {
        BindPhases();
        if (!IsConfigured)
            Debug.LogError("Item use: assign a configured Target Resolver and Preview on an active host with BagHUD and its inventory.", this);
    }

    private void OnDisable()
    {
        if (_bag != null) _bag.OnItemSelected -= HandleItemSelected;
        _confirm.performed -= QueueConfirmation;
        _cancel.performed -= HandleCancel;
        _confirm.Disable();
        _cancel.Disable();
        _impactTween?.Kill();
        Cancel();
        UnbindPhases();
    }

    private void OnDestroy()
    {
        _impactTween?.Kill();
        _confirm.Dispose();
        _cancel.Dispose();
    }

    public bool TryBegin(ItemDefinition item)
    {
        if (_usingTeam || _bag == null || _bag.InventoryProvider == null || !CanUseItems()) return false;
        bool team = item != null && item.TargetsTeam;
        if (!team && !IsConfigured) return false;
        ItemInventoryProvider provider = _bag.InventoryProvider;
        if (!provider.EnsureInitialized()) return false;
        if (_session == null) _session = new ItemUseSession(provider.Inventory, CanUseItems);
        if (team)
        {
            _usingTeam = true;
            _teamCancelled = false;
            try
            {
                bool used = _session.TryUseTeam(item);
                if (used)
                {
                    AudioManager.Instance.PlayUiStartGame();
                    PlayImpactBurst(item, new Vector2(Screen.width * 0.5f, Screen.height * 0.52f));
                    _bag.Close();
                    Cancel();
                }
                return used;
            }
            finally
            {
                _usingTeam = false;
            }
        }
        if (!_session.TryBegin(item, Time.frameCount)) return false;

        // Acquire before closing the bag so ownership never drops between modes.
        _phases.InputGate.Acquire(this);
        _bag.Close();
        _bag.SetAimingItem(item);
        ConfigureAimVisuals(item);
        _confirmPending = false;
        UpdatePreview();
        return true;
    }

    public void Cancel()
    {
        if (_usingTeam) _teamCancelled = true;
        _session?.Cancel();
        _confirmPending = false;
        if (_preview != null) _preview.Hide();
        HideAimFeedback();
        if (_bag != null) _bag.SetAimingItem(null);
        if (_phases != null) _phases.InputGate.Release(this, Time.frameCount);
    }

    private bool CanUseItems()
    {
        if (_usingTeam && _teamCancelled) return false;
        if (!isActiveAndEnabled || _bag == null || !_bag.isActiveAndEnabled) return false;
        if (_phases == null || !_phases.GameplayPhaseAllowsInput) return false;

        object owner = _bag.IsOpen ? (object)_bag : this;
        return !_phases.InputGate.HasOtherOwners(owner);
    }

    private void HandleItemSelected(ItemDefinition item) => TryBegin(item);
    private void HandleCancel(InputAction.CallbackContext context) => Cancel();
    private void HandleReward(int number) => Cancel();

    private void QueueConfirmation(InputAction.CallbackContext context)
    {
        if (!IsTargeting || Mouse.current == null) return;
        _pendingPosition = Mouse.current.position.ReadValue();
        _confirmPending = true;
    }

    private void LateUpdate()
    {
        if (!IsTargeting) return;
        if (!CanUseItems() || !_session.HasStock || !_targetResolver.isActiveAndEnabled || !_preview.isActiveAndEnabled)
        {
            Cancel();
            return;
        }

        // Resolve after the EventSystem so a UI click never becomes a world use.
        if (_confirmPending)
        {
            _confirmPending = false;
            ConfirmAt(_pendingPosition);
        }
        if (IsTargeting) UpdatePreview();
    }

    private void ConfirmAt(Vector2 position)
    {
        if (IsPointerOverUI()) return;

        ItemDefinition usedItem = _session.SelectedItem;
        bool used;
        if (usedItem.Effect == ItemEffectType.InstantGrowth)
        {
            if (!_targetResolver.TryResolveTree(position, out TreeInteractable tree)) return;
            used = _session.TryUseTree(tree, Time.frameCount);
        }
        else
        {
            if (!_targetResolver.TryResolve(position, out Vector3 point)) return;
            used = _session.TryUse(point, Time.frameCount);
        }

        if (used)
        {
            AudioManager.Instance.PlayUiStartGame();
            PlayImpactBurst(usedItem, position);
            Cancel();
        }
    }

    private void UpdatePreview()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || IsPointerOverUI())
        {
            _preview.Hide();
            HideAimFeedback();
            return;
        }

        Vector2 position = mouse.position.ReadValue();
        if (_session.SelectedItem.Effect == ItemEffectType.InstantGrowth)
        {
            UpdateTreePreview(position);
            return;
        }
        if (!_targetResolver.TryResolve(position, out Vector3 point))
        {
            _preview.Hide();
            HideAimFeedback();
            return;
        }

        bool valid = _session.HasTargets(point);
        _preview.Show(point, _session.SelectedItem.Radius, valid);
        UpdateAimFeedback(point, position, valid);
    }

    private void UpdateTreePreview(Vector2 position)
    {
        if (_targetResolver.TryResolveTree(position, out TreeInteractable tree))
        {
            bool valid = _session.HasTreeTarget(tree);
            _preview.Show(tree.transform.position, _singleTreePreviewRadius, valid);
            UpdateAimFeedback(tree.transform.position, position, valid);
        }
        else if (_targetResolver.TryResolve(position, out Vector3 point))
        {
            _preview.Show(point, _singleTreePreviewRadius, false);
            UpdateAimFeedback(point, position, false);
        }
        else
        {
            _preview.Hide();
            HideAimFeedback();
        }
    }

    private void ConfigureAimVisuals(ItemDefinition item)
    {
        if (item == null) return;

        if (_aimTooltipIcon != null)
        {
            _aimTooltipIcon.sprite = item.Icon;
            _aimTooltipIcon.enabled = item.Icon != null;
        }

        if (_aimTooltipText != null)
        {
            string upperTitle = item.Title.ToUpperInvariant();
            string verb = item.Effect switch
            {
                ItemEffectType.AreaDamage => "throw",
                ItemEffectType.AreaFreeze => "freeze",
                ItemEffectType.InstantGrowth => "grow tree",
                ItemEffectType.TreeInvulnerability => "shield trees",
                _ => "use"
            };
            _aimTooltipText.text = $"<b>{upperTitle}.</b> Click to {verb}, right click to cancel";
        }

        if (_targetPinIcon != null)
        {
            _targetPinIcon.sprite = item.Icon;
            _targetPinIcon.enabled = item.Icon != null;
        }
    }

    private void UpdateAimFeedback(Vector3 worldPoint, Vector2 mouseScreenPos, bool valid)
    {
        if (_hudCanvas == null) return;

        Vector2 anchorScreenPos = mouseScreenPos;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 projected = mainCam.WorldToScreenPoint(worldPoint);
            if (projected.z > 0f)
                anchorScreenPos = new Vector2(projected.x, projected.y);
        }

        if (_targetPinRoot != null)
        {
            _targetPinRoot.gameObject.SetActive(true);
            if (TryScreenToCanvasLocal(anchorScreenPos, out Vector2 pinLocal))
                _targetPinRoot.anchoredPosition = pinLocal;

            if (_targetPinImage != null)
            {
                Sprite pinSprite = valid ? _targetPinValidSprite : _targetPinInvalidSprite;
                if (pinSprite != null) _targetPinImage.sprite = pinSprite;
            }
        }

        if (_aimTooltipRoot != null)
        {
            _aimTooltipRoot.gameObject.SetActive(true);
            if (TryScreenToCanvasLocal(anchorScreenPos, out Vector2 tipLocal))
            {
                RectTransform canvasRect = (RectTransform)_hudCanvas.transform;
                Rect bounds = canvasRect.rect;
                Vector2 half = _aimTooltipRoot.rect.size * 0.5f;
                Vector2 desired = tipLocal + _aimTooltipOffset;

                // Flip tooltip to the left if near the right edge
                if (desired.x + half.x > bounds.xMax - 16f)
                    desired.x = tipLocal.x - _aimTooltipOffset.x;

                desired.x = Mathf.Clamp(desired.x, bounds.xMin + half.x + 16f, bounds.xMax - half.x - 16f);
                desired.y = Mathf.Clamp(desired.y, bounds.yMin + half.y + 16f, bounds.yMax - half.y - 16f);
                _aimTooltipRoot.anchoredPosition = desired;
            }
        }
    }

    private void HideAimFeedback()
    {
        if (_aimTooltipRoot != null) _aimTooltipRoot.gameObject.SetActive(false);
        if (_targetPinRoot != null) _targetPinRoot.gameObject.SetActive(false);
    }

    private void PlayImpactBurst(ItemDefinition item, Vector2 screenPosition)
    {
        if (_impactBurstRoot == null || _hudCanvas == null) return;

        if (_impactBurstText != null && item != null)
        {
            _impactBurstText.text = item.Effect switch
            {
                ItemEffectType.AreaDamage => "BOOM!",
                ItemEffectType.AreaFreeze => "FREEZE!",
                ItemEffectType.InstantGrowth => "GROW!",
                ItemEffectType.TreeInvulnerability => "SHIELD!",
                ItemEffectType.TeamHaste => "HASTE!",
                ItemEffectType.TeamEmpowerment => "POWER!",
                _ => "USED!"
            };
        }

        if (TryScreenToCanvasLocal(screenPosition, out Vector2 localPos))
            _impactBurstRoot.anchoredPosition = localPos;

        _impactTween?.Kill();
        _impactBurstRoot.gameObject.SetActive(true);
        _impactBurstRoot.localScale = Vector3.one * 0.45f;
        if (_impactBurstGroup != null) _impactBurstGroup.alpha = 1f;

        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(_impactBurstRoot.gameObject);
        seq.Append(_impactBurstRoot.DOScale(1.12f, 0.16f).SetEase(Ease.OutBack));
        seq.Append(_impactBurstRoot.DOScale(1f, 0.08f).SetEase(Ease.InOutQuad));
        seq.AppendInterval(0.38f);
        if (_impactBurstGroup != null)
            seq.Append(_impactBurstGroup.DOFade(0f, 0.28f));
        seq.Join(_impactBurstRoot.DOScale(1.22f, 0.28f).SetEase(Ease.OutQuad));
        seq.OnComplete(() =>
        {
            if (_impactBurstRoot != null)
                _impactBurstRoot.gameObject.SetActive(false);
        });
        _impactTween = seq;
    }

    private bool TryScreenToCanvasLocal(Vector2 screenPos, out Vector2 localPos)
    {
        localPos = Vector2.zero;
        if (_hudCanvas == null) return false;
        RectTransform canvasRect = (RectTransform)_hudCanvas.transform;
        Camera uiCam = _hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _hudCanvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCam, out localPos);
    }

    private static bool IsPointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void BindPhases()
    {
        PhaseManager phases = PhaseManager.Instance;
        if (_phases == phases) return;
        Cancel();
        UnbindPhases();
        _phases = phases;
        if (_phases == null) return;
        _phases.OnRewardStart += HandleReward;
        _phases.OnRunFinished += Cancel;
    }

    private void UnbindPhases()
    {
        if (_phases == null) return;
        _phases.OnRewardStart -= HandleReward;
        _phases.OnRunFinished -= Cancel;
        _phases = null;
    }
}
