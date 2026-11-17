using System;
using UnityEngine;

[Serializable]
public class LightingPreset
{
    [SerializeField] private Color _lightColor = Color.white;
    [SerializeField] private float _lightIntensity = 1f;
    [SerializeField] private Vector3 _lightEuler = new Vector3(50f, -30f, 0f);
    [SerializeField] private Color _ambientColor = Color.gray;
    [SerializeField] private Color _backgroundColor = Color.black;

    public Color LightColor => _lightColor;
    public float LightIntensity => _lightIntensity;
    public Quaternion LightRotation => Quaternion.Euler(_lightEuler);
    public Color AmbientColor => _ambientColor;
    public Color BackgroundColor => _backgroundColor;
}

[CreateAssetMenu(fileName = "DayNightLightingSettings", menuName = "Game/Day Night Lighting Settings")]
public class DayNightLightingSettings : ScriptableObject
{
    // TODO(balance): all colors, intensities and angles are placeholders.
    [SerializeField] private LightingPreset _day;
    [SerializeField] private LightingPreset _night;
    // TODO(balance): placeholder transition length in seconds.
    [SerializeField] private float _transitionDuration = 2f;

    public LightingPreset Day => _day;
    public LightingPreset Night => _night;
    public float TransitionDuration => _transitionDuration;
}
