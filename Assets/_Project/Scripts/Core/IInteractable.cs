using UnityEngine;

// What a spirit needs from any target (grid, tree, ...). Spirits never see concrete types.
public interface IInteractable
{
    // World point the spirit walks to before interacting.
    Vector3 InteractionPoint { get; }

    // False when there is nothing to do (already clean, already finished).
    bool CanInteract { get; }

    // Called every frame while a spirit works here. Progress lives in the target,
    // so several spirits can share one target later.
    void Interact(SpiritController spirit, float deltaTime);
}