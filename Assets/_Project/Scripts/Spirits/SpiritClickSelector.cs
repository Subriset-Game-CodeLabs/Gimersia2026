using UnityEngine;
using UnityEngine.InputSystem;

public class SpiritClickSelector : MonoBehaviour
{
    [SerializeField] private InputActionReference _clickAction;
    [SerializeField] private SpiritSelection _selection;
    [SerializeField] private SpiritCommander _commander;
    [SerializeField] private IslandSpawner _island;
    [SerializeField] private float _maxDistance = 200f;

    private Camera _camera;

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void OnEnable()
    {
        _clickAction.action.Enable();
        _clickAction.action.performed += OnClick;
    }

    private void OnDisable()
    {
        _clickAction.action.performed -= OnClick;
        _clickAction.action.Disable();
    }

    private void OnClick(InputAction.CallbackContext context)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());

        // Collide: the spirit's click collider is a trigger.
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, ~0, QueryTriggerInteraction.Collide)) return;
        SpiritView spirit = hit.collider.GetComponentInParent<SpiritView>();
        if (spirit != null)
        {
            _selection.SelectOnly(spirit);
            return;
        }

        // Interactables first: grids and (later) trees. Done targets fall through to a plain move.
        IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
        if (interactable != null && interactable.CanInteract)
        {
            _commander.InteractSelected(interactable);
            return;
        }

        // Only tiles are valid move targets.
        if (_island.GetViewFromCollider(hit.collider) != null)
        {
            _commander.MoveSelectedTo(hit.point);
        }
    }

}
