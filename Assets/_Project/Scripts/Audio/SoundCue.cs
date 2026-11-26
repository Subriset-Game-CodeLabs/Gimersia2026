using System;
using UnityEngine;

[Serializable]
public class SoundCue
{
    [Tooltip("Bisa diisi 1 klip atau lebih; jika lebih dari 1 akan diputar secara acak.")]
    public AudioClip[] clips;

    [Range(0f, 1.5f)]
    public float volume = 1f;

    [Tooltip("Rentang variasi pitch acak (X = min, Y = max) agar suara berulang terdengar alami.")]
    public Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Tooltip("Jeda minimum (detik) sebelum suara yang sama boleh diputar lagi.")]
    [Min(0f)]
    public float cooldown = 0f;

    [NonSerialized] private float _lastPlayedUnscaledTime = -999f;

    public bool HasClip
    {
        get
        {
            if (clips == null || clips.Length == 0) return false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null) return true;
            }
            return false;
        }
    }

    public bool TryConsumeClip(out AudioClip clip, out float pitch)
    {
        clip = null;
        pitch = 1f;

        if (!HasClip) return false;

        if (cooldown > 0f && Time.unscaledTime - _lastPlayedUnscaledTime < cooldown)
            return false;

        // Pilih klip valid secara acak
        int startIndex = UnityEngine.Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip candidate = clips[(startIndex + i) % clips.Length];
            if (candidate != null)
            {
                clip = candidate;
                break;
            }
        }

        if (clip == null) return false;

        float minPitch = Mathf.Clamp(Mathf.Min(pitchRange.x, pitchRange.y), 0.1f, 3f);
        float maxPitch = Mathf.Clamp(Mathf.Max(pitchRange.x, pitchRange.y), 0.1f, 3f);
        pitch = UnityEngine.Random.Range(minPitch, maxPitch);

        _lastPlayedUnscaledTime = Time.unscaledTime;
        return true;
    }
}