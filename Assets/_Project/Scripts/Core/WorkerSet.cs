using System.Collections.Generic;
using UnityEngine;

// The spirits currently working on one target. Plain C#, one per target.
// Also splits the teamwork bonus, so grid and tree share the same math.
public class WorkerSet
{
    private readonly TeamworkSettings _settings;
    private readonly List<SpiritController> _workers = new List<SpiritController>();

    public int Count => _workers.Count;

    public WorkerSet(TeamworkSettings settings)
    {
        _settings = settings;
    }

    public void Add(SpiritController spirit)
    {
        if (_workers.Contains(spirit)) return; // never count a spirit twice
        _workers.Add(spirit);
    }

    public void Remove(SpiritController spirit)
    {
        _workers.Remove(spirit);
    }

    // Work one spirit adds this frame. All workers together add deltaTime * multiplier.
    public float GetShare(float deltaTime)
    {
        int count = Mathf.Max(1, _workers.Count);
        return deltaTime * _settings.GetMultiplier(count) / count;
    }
}