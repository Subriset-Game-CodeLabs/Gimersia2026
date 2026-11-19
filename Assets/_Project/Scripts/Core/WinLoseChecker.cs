using System;
using UnityEngine;

public enum RunResult { Playing, Won, Lost }

public class WinLoseChecker : MonoBehaviour
{
    public RunResult Result { get; private set; } = RunResult.Playing;
    public event Action<RunResult> OnResult;

    private PhaseManager _phases;
    private TreeRegistry _registry;

    private void Start()
    {
        // Subscribe in Start, same as the other phase listeners.
        _phases = PhaseManager.Instance;
        _registry = TreeRegistry.Instance;
        _registry.OnTreeDied += HandleTreeDied;
        _phases.OnRunFinished += HandleRunFinished;
    }

    private void OnDestroy()
    {
        if (_registry != null) _registry.OnTreeDied -= HandleTreeDied;
        if (_phases != null) _phases.OnRunFinished -= HandleRunFinished;
        // Leaving Play mode must never leave the game frozen.
        Time.timeScale = 1f;
    }

    private void HandleTreeDied(TreeNode tree)
    {
        // Lose only when the count reaches 0, so an empty start is not a loss.
        if (_registry.AliveCount == 0) Finish(RunResult.Lost);
    }

    private void HandleRunFinished()
    {
        // Night 7 ended: at least 1 tree alive wins, otherwise it is a loss.
        Finish(_registry.AliveCount > 0 ? RunResult.Won : RunResult.Lost);
    }

    private void Finish(RunResult result)
    {
        // The first result wins, so a late event cannot overwrite it.
        if (Result != RunResult.Playing) return;
        Result = result;
        Debug.Log($"[Run] Result: {result}");
        Time.timeScale = 0f; // ResultHud displays the end screen; SceneFlow restores timeScale on restart/menu.
        OnResult?.Invoke(result);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Force Win")]
    private void DebugForceWin()
    {
        if (Application.isPlaying) Finish(RunResult.Won);
    }

    [ContextMenu("Debug/Force Lose")]
    private void DebugForceLose()
    {
        if (Application.isPlaying) Finish(RunResult.Lost);
    }
#endif
}
