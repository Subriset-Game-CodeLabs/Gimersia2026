using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpiritKeySelector : MonoBehaviour
{
    [SerializeField] private InputActionReference _select1Action;
    [SerializeField] private InputActionReference _select2Action;
    [SerializeField] private InputActionReference _select3Action;
    [SerializeField] private InputActionReference _selectAllAction;
    [SerializeField] private SpiritSelection _selection;

    private readonly bool[] _keyPressedStates = new bool[4];

    /// <summary>
    /// Slot: 0 = Key 1, 1 = Key 2, 2 = Key 3, 3 = Key Space (Select All)
    /// </summary>
    public event Action<int, bool> OnKeyStateChanged;

    public bool IsSlotPressed(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _keyPressedStates.Length) return false;
        return _keyPressedStates[slotIndex];
    }

    private void OnEnable()
    {
        Subscribe(_select1Action, OnSelect1Performed, OnSelect1Started, OnSelect1Canceled);
        Subscribe(_select2Action, OnSelect2Performed, OnSelect2Started, OnSelect2Canceled);
        Subscribe(_select3Action, OnSelect3Performed, OnSelect3Started, OnSelect3Canceled);
        Subscribe(_selectAllAction, OnSelectAllPerformed, OnSelectAllStarted, OnSelectAllCanceled);
    }

    private void OnDisable()
    {
        Unsubscribe(_select1Action, OnSelect1Performed, OnSelect1Started, OnSelect1Canceled);
        Unsubscribe(_select2Action, OnSelect2Performed, OnSelect2Started, OnSelect2Canceled);
        Unsubscribe(_select3Action, OnSelect3Performed, OnSelect3Started, OnSelect3Canceled);
        Unsubscribe(_selectAllAction, OnSelectAllPerformed, OnSelectAllStarted, OnSelectAllCanceled);

        for (int i = 0; i < _keyPressedStates.Length; i++)
            SetSlotPressed(i, false);
    }

    private static void Subscribe(
        InputActionReference reference,
        Action<InputAction.CallbackContext> onPerformed,
        Action<InputAction.CallbackContext> onStarted,
        Action<InputAction.CallbackContext> onCanceled)
    {
        if (reference == null || reference.action == null) return;
        reference.action.Enable();
        reference.action.performed += onPerformed;
        reference.action.started += onStarted;
        reference.action.canceled += onCanceled;
    }

    private static void Unsubscribe(
        InputActionReference reference,
        Action<InputAction.CallbackContext> onPerformed,
        Action<InputAction.CallbackContext> onStarted,
        Action<InputAction.CallbackContext> onCanceled)
    {
        if (reference == null || reference.action == null) return;
        reference.action.performed -= onPerformed;
        reference.action.started -= onStarted;
        reference.action.canceled -= onCanceled;
        reference.action.Disable();
    }

    private void SetSlotPressed(int slotIndex, bool isPressed)
    {
        if (_keyPressedStates[slotIndex] == isPressed) return;
        _keyPressedStates[slotIndex] = isPressed;
        OnKeyStateChanged?.Invoke(slotIndex, isPressed);
    }

    // Slot 0: Key 1
    private void OnSelect1Started(InputAction.CallbackContext ctx) => SetSlotPressed(0, true);
    private void OnSelect1Canceled(InputAction.CallbackContext ctx) => SetSlotPressed(0, false);
    private void OnSelect1Performed(InputAction.CallbackContext ctx) => SelectIndex(0);

    // Slot 1: Key 2
    private void OnSelect2Started(InputAction.CallbackContext ctx) => SetSlotPressed(1, true);
    private void OnSelect2Canceled(InputAction.CallbackContext ctx) => SetSlotPressed(1, false);
    private void OnSelect2Performed(InputAction.CallbackContext ctx) => SelectIndex(1);

    // Slot 2: Key 3
    private void OnSelect3Started(InputAction.CallbackContext ctx) => SetSlotPressed(2, true);
    private void OnSelect3Canceled(InputAction.CallbackContext ctx) => SetSlotPressed(2, false);
    private void OnSelect3Performed(InputAction.CallbackContext ctx) => SelectIndex(2);

    // Slot 3: Space (Toggle Select All / Cancel All)
    private void OnSelectAllStarted(InputAction.CallbackContext ctx) => SetSlotPressed(3, true);
    private void OnSelectAllCanceled(InputAction.CallbackContext ctx) => SetSlotPressed(3, false);
    private void OnSelectAllPerformed(InputAction.CallbackContext ctx)
    {
        if (_selection != null)
            _selection.ToggleSelectAll();
    }

    private void SelectIndex(int index)
    {
        if (_selection == null) return;

        bool isLeftShiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        if (isLeftShiftHeld)
        {
            _selection.ToggleMultiByIndex(index);
        }
        else
        {
            _selection.ToggleSingleByIndex(index);
        }
    }
}