using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShopRuntime
{
    public static ShopProgress Progress { get; private set; }
    public static SelectedStage SelectedStage { get; private set; }

    private static ShopSaveStore CreateSaveStore() => new ShopSaveStore(
        Path.Combine(Application.persistentDataPath, "preparation-shop-v1.json"));

    // The menu precedes the shop config. Read only: never guess starting coins.
    public static bool TryLoadProgression(out HashSet<string> completedLevelIds, out string error)
    {
        completedLevelIds = null;
        error = null;
        if (Progress != null)
        {
            completedLevelIds = Progress.CopyCompletedLevelIds();
            return true;
        }
        if (!CreateSaveStore().TryLoad(out ShopSaveData data, out bool exists, out error)) return false;
        completedLevelIds = new HashSet<string>(System.StringComparer.Ordinal);
        if (exists && data.completedLevelIds != null) completedLevelIds.UnionWith(data.completedLevelIds);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Progress = null;
        SelectedStage = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static bool TrySelectStage(string id, string sceneName, int winCoins, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || string.IsNullOrWhiteSpace(sceneName) || winCoins < 0)
        { error = "Configure a stable level ID, gameplay scene and nonnegative victory coins."; return false; }
        SelectedStage = new SelectedStage(id, sceneName, winCoins);
        return true;
    }

    public static bool TryInitialize(PreparationShopConfig config, out string error)
    {
        error = null;
        if (config == null) { error = "Assign the preparation shop config."; return false; }
        if (!config.TryValidate(out error)) return false;
        if (Progress != null) return true;
        if (!ShopProgress.TryOpen(CreateSaveStore(), config.InitialCoins, out ShopProgress progress, out error)) return false;
        Progress = progress;
        Progress.OnSaveError += ReportError;
        if (error != null) Debug.LogWarning(error);
        return true;
    }

    public static bool TryCheckpoint()
    {
        if (Progress == null) return true;
        if (Progress.TryFlush(out string error)) return true;
        ReportError(error);
        return false;
    }

    private static void ReportError(string error) => Debug.LogError("Preparation shop: " + error);

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var host = new GameObject("Shop Session Checkpoints");
        SceneManager.MoveGameObjectToScene(host, scene);
        var adapter = host.AddComponent<ShopSceneAdapter>();
        // Each completed gameplay scene load (including Retry) is a fresh run.
        bool gameplay = SelectedStage != null && scene.name == SelectedStage.SceneName;
        Progress?.BeginRun(gameplay ? SelectedStage : null);
        adapter.Bind(gameplay ? Object.FindAnyObjectByType<WinLoseChecker>() : null);
    }
}
