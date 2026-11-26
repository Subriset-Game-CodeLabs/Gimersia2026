using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class LightingPreset
{
    [Header("Directional Light (Sun / Moon)")]
    [SerializeField] private Color _lightColor = Color.white;
    [SerializeField] private float _lightIntensity = 1.2f;
    [SerializeField] private Vector3 _lightEuler = new Vector3(52f, -35f, 0f);
    [SerializeField, Range(0f, 1f)] private float _shadowStrength = 0.85f;

    [Header("Gradient Ambient (Sky / Equator / Ground)")]
    [FormerlySerializedAs("_ambientColor")]
    [SerializeField] private Color _ambientSkyColor = new Color(0.55f, 0.72f, 0.95f, 1f);
    [SerializeField] private Color _ambientEquatorColor = new Color(0.75f, 0.82f, 0.88f, 1f);
    [SerializeField] private Color _ambientGroundColor = new Color(0.28f, 0.34f, 0.28f, 1f);

    [Header("Camera Background")]
    [SerializeField] private Color _backgroundColor = new Color(0.18f, 0.35f, 0.58f, 1f);

    public Color LightColor => _lightColor;
    public float LightIntensity => _lightIntensity;
    public Quaternion LightRotation => Quaternion.Euler(_lightEuler);
    public float ShadowStrength => _shadowStrength;
    public Color AmbientSkyColor => _ambientSkyColor;
    public Color AmbientEquatorColor => _ambientEquatorColor;
    public Color AmbientGroundColor => _ambientGroundColor;
    public Color BackgroundColor => _backgroundColor;
}

[CreateAssetMenu(fileName = "DayNightLightingSettings", menuName = "Game/Day Night Lighting Settings")]
public class DayNightLightingSettings : ScriptableObject
{
    [SerializeField] private LightingPreset _day = new LightingPreset();
    [SerializeField] private LightingPreset _night = new LightingPreset();
    [SerializeField, Min(0f)] private float _transitionDuration = 2.2f;

    public LightingPreset Day => _day;
    public LightingPreset Night => _night;
    public float TransitionDuration => _transitionDuration;
}