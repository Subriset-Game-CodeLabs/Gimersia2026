using UnityEngine;

// Created per scene by ShopRuntime; no MonoBehaviour singleton or second inventory.
public sealed class ShopSceneAdapter : MonoBehaviour
{
    private WinLoseChecker _checker;
    private ShopProgress _progress;
    private float _nextCheckpoint;
    private bool _pendingResult;
    private bool _won;

    public void Bind(WinLoseChecker checker)
    {
        _checker = checker;
        if (_checker != null) _checker.OnResult += HandleResult;
    }

    private void HandleResult(RunResult result)
    {
        if (result == RunResult.Playing) return;
        _pendingResult = true;
        _won = result == RunResult.Won;
        SaveResult();
    }

    private void SaveResult()
    {
        if (ShopRuntime.Progress == null) { _pendingResult = false; return; }
        if (ShopRuntime.Progress.TryFinishRun(_won, out string error) || error == null)
            _pendingResult = false;
        // A failed disk write remains pending and is retried on the next checkpoint.
        _nextCheckpoint = Time.unscaledTime + 1f;
    }

    private void LateUpdate()
    {
        if (_progress != ShopRuntime.Progress)
        {
            if (_progress != null) _progress.OnChanged -= ScheduleCheckpoint;
            _progress = ShopRuntime.Progress;
            if (_progress != null) _progress.OnChanged += ScheduleCheckpoint;
            ScheduleCheckpoint();
        }
        if (Time.unscaledTime < _nextCheckpoint) return;
        if (_pendingResult) SaveResult();
        if (_progress != null && _progress.IsDirty)
        {
            ShopRuntime.TryCheckpoint();
            _nextCheckpoint = Time.unscaledTime + 1f;
        }
    }

    private void ScheduleCheckpoint() => _nextCheckpoint = Time.unscaledTime + 0.35f;
    private void OnApplicationPause(bool paused) { if (paused) Checkpoint(); }
    private void OnApplicationQuit() => Checkpoint();
    private void Checkpoint()
    {
        if (_pendingResult) SaveResult();
        ShopRuntime.TryCheckpoint();
    }
    private void OnDestroy()
    {
        if (_checker != null) _checker.OnResult -= HandleResult;
        if (_progress != null) _progress.OnChanged -= ScheduleCheckpoint;
        Checkpoint();
    }
}
