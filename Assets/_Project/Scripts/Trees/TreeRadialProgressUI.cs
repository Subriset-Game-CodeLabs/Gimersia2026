using UnityEngine;
using UnityEngine.UI;

public class TreeRadialProgressUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TreeNode _treeNode;
    [SerializeField] private TreeInteractable _growthInteractable;
    [SerializeField] private TreeElementInteractable _elementInteractable;
    [SerializeField] private GameObject _visualRoot;
    [SerializeField] private Image _radialFillImage;

    [Header("Colors")]
    [SerializeField] private Color _growthProgressColor = new Color(0.35f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color _elementChangeColor = new Color(0.3f, 0.75f, 1f, 1f);

    [Header("Billboard")]
    [SerializeField] private bool _faceCamera = true;

    private Camera _mainCamera;

    private void Awake()
    {
        if (_treeNode == null) _treeNode = GetComponentInParent<TreeNode>();
        if (_growthInteractable == null && _treeNode != null)
            _growthInteractable = _treeNode.GetComponent<TreeInteractable>();
        if (_elementInteractable == null && _treeNode != null)
            _elementInteractable = _treeNode.GetComponent<TreeElementInteractable>();

        _mainCamera = Camera.main;
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (_treeNode == null || !_treeNode.IsTargetable)
        {
            SetVisible(false);
            return;
        }

        // Prioritaskan progres penggantian elemen jika pohon sudah Complete
        if (_elementInteractable != null && _elementInteractable.CanInteract &&
            (_elementInteractable.IsWorking || _elementInteractable.Progress01 > 0.001f))
        {
            SetVisible(true);
            UpdateVisual(_elementInteractable.Progress01, _elementChangeColor);
            OrientToCamera();
            return;
        }

        // Progres pertumbuhan fase pohon
        if (_growthInteractable != null && _growthInteractable.CanInteract &&
            (_growthInteractable.IsWorking || _growthInteractable.Progress01 > 0.001f))
        {
            SetVisible(true);
            UpdateVisual(_growthInteractable.Progress01, _growthProgressColor);
            OrientToCamera();
            return;
        }

        SetVisible(false);
    }

    private void UpdateVisual(float fill01, Color barColor)
    {
        if (_radialFillImage == null) return;
        _radialFillImage.fillAmount = Mathf.Clamp01(fill01);
        _radialFillImage.color = barColor;
    }

    private void SetVisible(bool visible)
    {
        if (_visualRoot != null && _visualRoot.activeSelf != visible)
            _visualRoot.SetActive(visible);
    }

    private void OrientToCamera()
    {
        if (!_faceCamera) return;
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera != null)
            transform.rotation = _mainCamera.transform.rotation;
    }
}