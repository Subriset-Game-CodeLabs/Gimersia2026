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

    private void Awake()
    {
        // Idle spirits do no per-frame work.
        enabled = false;
    }

    public void MoveTo(Vector3 worldPoint)
    {
        _interactable = null;
        BeginMove(worldPoint);
    }

    public void Interact(IInteractable target)
    {
        if (target == null || !target.CanInteract) return;

        _interactable = target;
        BeginMove(target.InteractionPoint);
    }

    private void BeginMove(Vector3 worldPoint)
    {
        // Stay on the spirit's own height so it slides along the ground.
        _target = new Vector3(worldPoint.x, transform.position.y, worldPoint.z);
        State = SpiritState.Moving;
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

        if (_interactable != null && _interactable.CanInteract) State = SpiritState.Interacting;
        else Stop();
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
        _interactable = null;
        State = SpiritState.Idle;
        enabled = false;
    }
}
