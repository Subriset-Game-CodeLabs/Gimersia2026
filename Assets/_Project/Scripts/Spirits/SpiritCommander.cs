using UnityEngine;

public class SpiritCommander : MonoBehaviour
{
    [SerializeField] private SpiritSelection _selection;

    public void MoveSelectedTo(Vector3 worldPoint)
    {
        foreach (SpiritView spirit in _selection.Selected)
        {
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controller.MoveTo(worldPoint);
        }
    }

    public void InteractSelected(IInteractable target)
    {
        foreach (SpiritView spirit in _selection.Selected)
        {
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controller.Interact(target);
        }
    }

    public void AttackSelected(EnemyController enemy, Bounds islandBounds)
    {
        foreach (SpiritView spirit in _selection.Selected)
        {
            SpiritController controller = spirit.GetComponent<SpiritController>();
            if (controller != null) controller.Attack(enemy, islandBounds);
        }
    }
}
