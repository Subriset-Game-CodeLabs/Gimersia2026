using System;
using System.Collections.Generic;

// Blocks new commands, never simulation or an already-running spirit order.
public sealed class GameplayInputGate
{
    private readonly HashSet<object> _owners = new HashSet<object>();
    private int _releasedFrame = -1;

    public bool IsBlocked(int frame) => _owners.Count > 0 || frame <= _releasedFrame;

    public bool HasOtherOwners(object owner)
    {
        foreach (object current in _owners)
        {
            if (!ReferenceEquals(current, owner)) return true;
        }
        return false;
    }

    public void Acquire(object owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        _owners.Add(owner);
    }

    public void Release(object owner, int frame)
    {
        if (owner != null && _owners.Remove(owner)) _releasedFrame = Math.Max(_releasedFrame, frame);
    }
}
