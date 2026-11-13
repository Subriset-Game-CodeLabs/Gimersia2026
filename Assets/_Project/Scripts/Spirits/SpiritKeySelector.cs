using UnityEngine;
using UnityEngine.InputSystem;

public class SpiritKeySelector : MonoBehaviour
{
    [SerializeField] private InputActionReference _select1Action;
    [SerializeField] private InputActionReference _select2Action;
    [SerializeField] private InputActionReference _select3Action;
    [SerializeField] private InputActionReference _selectAllAction;
    [SerializeField] private SpiritSelection _selection;
    [SerializeField] private CameraFocus _cameraFocus;

    private void OnEnable()
    {
        Subscribe(_select1Action, OnSelect1);
        Subscribe(_select2Action, OnSelect2);
        Subscribe(_select3Action, OnSelect3);
        Subscribe(_selectAllAction, OnSelectAll);
    }

    private void OnDisable()
    {
        Unsubscribe(_select1Action, OnSelect1);
        Unsubscribe(_select2Action, OnSelect2);
        Unsubscribe(_select3Action, OnSelect3);
        Unsubscribe(_selectAllAction, OnSelectAll);
    }

    private static void Subscribe(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
    {
        reference.action.Enable();
        reference.action.performed += handler;
    }

    private static void Unsubscribe(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
    {
        reference.action.performed -= handler;
        reference.action.Disable();
    }

    // Named handlers, so they can be unsubscribed (no lambdas).
    private void OnSelect1(InputAction.CallbackContext context) => SelectIndex(0);
    private void OnSelect2(InputAction.CallbackContext context) => SelectIndex(1);
    private void OnSelect3(InputAction.CallbackContext context) => SelectIndex(2);
    private void OnSelectAll(InputAction.CallbackContext context) => _selection.SelectAll();

    private void SelectIndex(int index)
    {
        SpiritView spirit = _selection.SelectByIndex(index);
        if (spirit != null) _cameraFocus.FocusOn(spirit.transform.position);
    }
}
