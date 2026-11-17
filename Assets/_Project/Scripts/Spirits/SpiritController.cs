using System;
using UnityEngine;

public enum SpiritState { Idle, Moving, Interacting }


public class SpiritController : MonoBehaviour
{
    // TODO(balance): placeholder move speed.
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _arriveDistance = 0.05f;

    private Vector3 _target;
    private IInteractable _interactable;

    public SpiritState State { get; private set; } = SpiritState.Idle;

    public event Action OnArrived;
    // For the animation task: one place to react to idle, moving and working.
    public event Action<SpiritState> OnStateChanged;

    private void Awake()
    {
        // Idle spirits do no per-frame work.
        enabled = false;
    }

    private void OnDestroy()
    {
        // Never leave a ghost worker on a target.
        SetTarget(null);
    }

    public void MoveTo(Vector3 worldPoint)
    {
        SetTarget(null);
        BeginMove(worldPoint);
    }

    public void Interact(IInteractable target)
    {
        if (target == null || !target.CanInteract) return;

        SetTarget(target);
        BeginMove(target.InteractionPoint);
    }

    // The only place the target changes. It unregisters from the old target first,
    // so every exit path (new order, finish, destroy) stays correct.
    // Remove is a no-op if the spirit was never registered (still walking).
    private void SetTarget(IInteractable target)
    {
        if (_interactable != null) _interactable.RemoveWorker(this);
        _interactable = target;
    }

    private void SetState(SpiritState state)
    {
        if (State == state) return;
        State = state;
        OnStateChanged?.Invoke(state);
    }

    private void BeginMove(Vector3 worldPoint)
    {
        // Stay on the spirit's own height so it slides along the ground.
        _target = new Vector3(worldPoint.x, transform.position.y, worldPoint.z);
        SetState(SpiritState.Moving);
        enabled = true;
    }

    private void Update()
    {
        switch (State)
        {
            case SpiritState.Moving:
                UpdateMoving();
                break;
            case SpiritState.Interacting:
                UpdateInteracting();
                break;
        }
    }

    private void UpdateMoving()
    {
        transform.position = Vector3.MoveTowards(transform.position, _target, _moveSpeed * Time.deltaTime);

        if ((transform.position - _target).sqrMagnitude > _arriveDistance * _arriveDistance) return;

        OnArrived?.Invoke();

        if (_interactable != null && _interactable.CanInteract)
        {
            // Register only on arrival, so walking spirits don't count for the multiplier.
            _interactable.AddWorker(this);
            SetState(SpiritState.Interacting);
        }
        else
        {
            Stop();
        }
    }

    private void UpdateInteracting()
    {
        // The target may finish while we work (or because another spirit finished it).
        if (!_interactable.CanInteract)
        {
            Stop();
            return;
        }

        _interactable.Interact(this, Time.deltaTime);
    }

    private void Stop()
    {
        SetTarget(null);
        SetState(SpiritState.Idle);
        enabled = false;
    }
}
