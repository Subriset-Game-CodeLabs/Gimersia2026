using UnityEngine;
using UnityEngine.SceneManagement;

// One place for every scene change, so time scale and scene names never drift apart.
public static class SceneFlow
{
    public const string MainMenuScene = "MainMenu";
    public const string LevelSelectionScene = "LevelSelection";
    public const string PreparationShopScene = "PreparationShop";
    public static bool IsLoading { get; private set; }
    public static event System.Action OnLoadingChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLoading()
    { IsLoading = false; OnLoadingChanged = null; }
    // TODO: point this at the final gameplay scene once it replaces the playground.
    public const string GameplayScene = "Hakim_playground";

    public static void LoadMainMenu() => Load(MainMenuScene);
    public static void LoadLevelSelection() => Load(LevelSelectionScene);

    public static void Load(string sceneName)
    {
        if (IsLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneFlow] Scene '{sceneName}' is not in the Build Profile scene list. " +
                           "Add the scene to the active Build Profile scene list.");
            return;
        }

        if (!ShopRuntime.TryCheckpoint()) return;
        BeginLoad(sceneName, false);
    }

    // AsyncOperation owns the callback; no scene-host coroutine is destroyed mid-load.
    private static void BeginLoad(string sceneName, bool editorPath)
    {
        float previousTimeScale = Time.timeScale;
        IsLoading = true;
        try
        {
            Time.timeScale = 1f;
            AsyncOperation operation;
#if UNITY_EDITOR
            if (editorPath)
                operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    sceneName, new LoadSceneParameters(LoadSceneMode.Single));
            else
#endif
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) throw new System.InvalidOperationException("Scene load did not start.");
            operation.completed += _ => { IsLoading = false; OnLoadingChanged?.Invoke(); };
            OnLoadingChanged?.Invoke();
        }
        catch (System.Exception exception)
        {
            IsLoading = false;
            Time.timeScale = previousTimeScale;
            Debug.LogError("Scene load failed: " + exception.Message);
            OnLoadingChanged?.Invoke();
        }
    }

    // Restart the level that is playing now, from scratch.
    public static void RestartCurrent()
    {
        if (IsLoading || !ShopRuntime.TryCheckpoint()) return;
        Scene scene = SceneManager.GetActiveScene();

#if UNITY_EDITOR
        // Lets a dev scene that is not in the build list restart in Play mode too.
        if (scene.buildIndex < 0)
        {
            BeginLoad(scene.path, true);
            return;
        }
#endif
        BeginLoad(scene.path, false);
    }

    public static void Quit()
    {
        if (IsLoading || !ShopRuntime.TryCheckpoint()) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
