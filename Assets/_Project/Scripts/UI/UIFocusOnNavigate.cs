using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Keeps UI selection empty until the player actually presses a directional key
// (Arrow keys / gamepad D-pad), so menus never open with a button pre-hovered.
// Reusable across Main Menu, Settings, Credit, Pause, Result, and Level Selection.
public class UIFocusOnNavigate : MonoBehaviour
{
    private Selectable _defaultTarget;

    // Sets which control gets focused on the first arrow press and clears any current selection.
    public void SetDefaultTarget(Selectable target, bool clearCurrentSelection = true)
    {
        _defaultTarget = target;
        if (clearCurrentSelection) ClearSelection();
    }

    public static void ClearSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // Runs in LateUpdate (after EventSystem.Update) so the first arrow press lands on
    // _defaultTarget instead of immediately stepping past it to the second control.
    private void LateUpdate()
    {
        if (_defaultTarget == null || !_defaultTarget.isActiveAndEnabled || !_defaultTarget.IsInteractable())
            return;

        EventSystem events = EventSystem.current;
        if (events == null) return;

        GameObject current = events.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy) return;

        if (!WasNavigatePressedThisFrame()) return;

        events.SetSelectedGameObject(_defaultTarget.gameObject);
    }

    private static bool WasNavigatePressedThisFrame()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (
            kb.upArrowKey.wasPressedThisFrame ||
            kb.downArrowKey.wasPressedThisFrame ||
            kb.leftArrowKey.wasPressedThisFrame ||
            kb.rightArrowKey.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null && (
            pad.dpad.up.wasPressedThisFrame ||
            pad.dpad.down.wasPressedThisFrame ||
            pad.dpad.left.wasPressedThisFrame ||
            pad.dpad.right.wasPressedThisFrame))
        {
            return true;
        }

        return false;
    }
}
