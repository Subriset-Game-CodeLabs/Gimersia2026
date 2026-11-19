using System;
using UnityEngine;

// Player volume preferences (0..1), saved in PlayerPrefs.
// The audio system reads these and listens to OnChanged; the settings pop-up only writes them.
public static class GameAudioSettings
{
    private const string MusicKey = "settings.musicVolume";
    private const string SfxKey = "settings.sfxVolume";
    private const float DefaultVolume = 0.8f;

    public static event Action OnChanged;

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, DefaultVolume);
        set => Set(MusicKey, value);
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, DefaultVolume);
        set => Set(SfxKey, value);
    }

    // Writes to disk. Call when the pop-up closes, not on every slider tick.
    public static void Save() => PlayerPrefs.Save();

    private static void Set(string key, float value)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
        OnChanged?.Invoke();
    }
}
