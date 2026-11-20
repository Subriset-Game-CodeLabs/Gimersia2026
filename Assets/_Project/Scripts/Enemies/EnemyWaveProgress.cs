using System;

// Plain runtime state: an empty battlefield is not a completed wave while spawns remain.
public sealed class EnemyWaveProgress
{
    public int RemainingSpawns { get; private set; }
    public int AliveCount { get; private set; }
    public bool IsActive { get; private set; }
    public bool HasFailed { get; private set; }
    private bool _finishedSpawning;

    public void Begin(int spawnCount)
    {
        if (spawnCount <= 0) throw new ArgumentOutOfRangeException(nameof(spawnCount));
        if (IsActive) throw new InvalidOperationException("A wave is already active.");
        RemainingSpawns = spawnCount;
        AliveCount = 0;
        HasFailed = false;
        _finishedSpawning = false;
        IsActive = true;
    }

    public void RegisterSpawn()
    {
        if (!IsActive || HasFailed || RemainingSpawns <= 0)
            throw new InvalidOperationException("No scheduled spawn is available.");
        RemainingSpawns--;
        AliveCount++;
    }

    public void RegisterDeath()
    {
        if (!IsActive || AliveCount <= 0)
            throw new InvalidOperationException("No live wave enemy is registered.");
        AliveCount--;
    }

    public void FinishSpawning()
    {
        if (!IsActive || RemainingSpawns != 0)
            throw new InvalidOperationException("Scheduled spawns have not finished.");
        _finishedSpawning = true;
    }

    public bool TryComplete()
    {
        if (!IsActive || HasFailed || !_finishedSpawning || RemainingSpawns != 0 || AliveCount != 0)
            return false;
        IsActive = false;
        return true;
    }

    public void Fail() => HasFailed = true;

    public void Cancel()
    {
        IsActive = false;
        RemainingSpawns = 0;
        AliveCount = 0;
        _finishedSpawning = false;
        HasFailed = false;
    }
}
