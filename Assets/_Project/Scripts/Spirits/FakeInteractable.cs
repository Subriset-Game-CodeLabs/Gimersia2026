using UnityEngine;

// --- TEMPORARY TEST: remove before finishing the task ---
// Stands in for a tree until trees exist.
public class FakeInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private float _duration = 2f;
    private float _progress;

    public Vector3 InteractionPoint => transform.position;
    public bool CanInteract => _progress < _duration;

    public void Interact(SpiritController spirit, float deltaTime)
    {
        _progress += deltaTime;
        if (_progress >= _duration) Debug.Log($"{name}: finished by {spirit.name}");
    }
}