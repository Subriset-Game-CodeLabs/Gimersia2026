using UnityEngine;
using UnityEngine.SceneManagement;

// One place for every scene change, so time scale and scene names never drift apart.
public static class SceneFlow
{
    public const string MainMenuScene = "MainMenu";
    // TODO: point this at the final gameplay scene once it replaces the playground.
    public const string GameplayScene = "Hakim_playground";

    public static void LoadMainMenu() => Load(MainMenuScene);

    public static void Load(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneFlow] Scene '{sceneName}' is not in the Build Profile scene list. " +
                           "Run Tools > Gimersia > Menu & Flow > Register Build Scenes.");
            return;
        }

        // The result screen pauses the game; a new scene must never start frozen.
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    // Restart the level that is playing now, from scratch.
    public static void RestartCurrent()
    {
        Scene scene = SceneManager.GetActiveScene();
        Time.timeScale = 1f;

#if UNITY_EDITOR
        // Lets a dev scene that is not in the build list restart in Play mode too.
        if (scene.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
