using UnityEngine;

public class SpiritCommander : MonoBehaviour
{
    [SerializeField] private SpiritSelection _selection;

    public void MoveSelectedTo(Vector3 worldPoint)
    {
        if (_selection == null || _selection.Selected.Count == 0) return;

        foreach (SpiritView spirit in _selection.Selected)
        {
            if (spirit == null) continue;
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controller.MoveTo(worldPoint);
        }

        // Otomatis lepas seleksi setelah Spirit diberi perintah bergerak
        _selection.ClearSelection();
    }

    public void InteractSelected(IInteractable target)
    {
        if (_selection == null || _selection.Selected.Count == 0) return;

        foreach (SpiritView spirit in _selection.Selected)
        {
            if (spirit == null) continue;
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controller.Interact(target);
        }

        // Otomatis lepas seleksi setelah Spirit diberi perintah interaksi
        _selection.ClearSelection();
    }

    public void AttackSelected(EnemyController enemy, Bounds islandBounds)
    {
        if (_selection == null || _selection.Selected.Count == 0) return;

        foreach (SpiritView spirit in _selection.Selected)
        {
            if (spirit == null) continue;
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controller.Attack(enemy, islandBounds);
        }

        // Otomatis lepas seleksi setelah Spirit diberi perintah menyerang
        _selection.ClearSelection();
    }
}