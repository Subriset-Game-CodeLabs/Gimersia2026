using System;
using System.Collections.Generic;
using UnityEngine;

public class NightSeaAtmosphereController : MonoBehaviour
{
    [Serializable]
    public class MaterialColorTransition
    {
        [Tooltip("Nama property warna di shader air (contoh: _BaseColor, _ShallowColor, _DeepColor, _FoamColor).")]
        public string propertyName = "_BaseColor";
        public Color dayColor = Color.white;
        public Color nightColor = new Color(0.15f, 0.25f, 0.45f, 1f);

        [HideInInspector] public int propertyId;
        [HideInInspector] public Color fromColor;
        [HideInInspector] public bool isValid;
    }

    [Header("1. Referensi Air Laut")]
    [Tooltip("Renderer mesh air laut di scene.")]
    [SerializeField] private Renderer _seaRenderer;
    [Tooltip("Daftar property warna pada shader air yang akan berubah saat Siang <-> Malam.")]
    [SerializeField] private List<MaterialColorTransition> _waterColorProperties = new List<MaterialColorTransition>
    {
        new MaterialColorTransition
        {
            propertyName = "_BaseColor",
            dayColor = new Color(0.16f, 0.48f, 0.82f, 1f),
            nightColor = new Color(0.04f, 0.11f, 0.26f, 1f)
        },
        new MaterialColorTransition
        {
            propertyName = "_FoamColor",
            dayColor = new Color(0.92f, 0.97f, 1.00f, 1f),
            nightColor = new Color(0.28f, 0.42f, 0.65f, 1f)
        }
    };

    [Header("2. Referensi Kabut Laut (Sea Mist)")]
    [Tooltip("Particle System kabut tipis yang mengelilingi laut di sekitar pulau.")]
    [SerializeField] private ParticleSystem _seaMistParticles;
    [SerializeField] private Color _dayMistColor = new Color(0.75f, 0.88f, 1f, 0.02f);
    [SerializeField] private Color _nightMistColor = new Color(0.45f, 0.62f, 0.92f, 0.14f);
    [SerializeField, Min(0f)] private float _dayMistEmissionRate = 2f;
    [SerializeField, Min(0f)] private float _nightMistEmissionRate = 14f;

    [Header("3. Durasi Transisi")]
    [SerializeField, Min(0f)] private float _transitionDuration = 2.2f;

    private PhaseManager _phases;
    private Material _seaMaterialInstance;
    private ParticleSystem.MainModule _mistMain;
    private ParticleSystem.EmissionModule _mistEmission;
    private bool _hasMist;

    private float _blend = 1f;
    private bool _targetIsNight;
    private Color _fromMistColor;
    private float _fromMistRate;

    private void Start()
    {
        InitializeSeaMaterial();
        InitializeSeaMist();

        _phases = PhaseManager.Instance;
        bool startNight = _phases != null && _phases.CurrentPhase == PhaseType.Night;
        ApplyInstant(startNight);

        if (_phases != null)
        {
            _phases.OnDayStart += HandleDayStart;
            _phases.OnNightStart += HandleNightStart;
        }

        enabled = false;
    }

    private void OnDestroy()
    {
        if (_phases != null)
        {
            _phases.OnDayStart -= HandleDayStart;
            _phases.OnNightStart -= HandleNightStart;
        }
    }

    private void InitializeSeaMaterial()
    {
        if (_seaRenderer == null) return;
        _seaMaterialInstance = _seaRenderer.material;
        if (_seaMaterialInstance == null) return;

        for (int i = 0; i < _waterColorProperties.Count; i++)
        {
            MaterialColorTransition entry = _waterColorProperties[i];
            if (string.IsNullOrWhiteSpace(entry.propertyName)) continue;

            entry.propertyId = Shader.PropertyToID(entry.propertyName);
            entry.isValid = _seaMaterialInstance.HasProperty(entry.propertyId);
        }
    }

    private void InitializeSeaMist()
    {
        if (_seaMistParticles == null) return;
        _mistMain = _seaMistParticles.main;
        _mistEmission = _seaMistParticles.emission;
        _hasMist = true;

        if (!_seaMistParticles.isPlaying)
            _seaMistParticles.Play();
    }

    private void HandleDayStart(int day) => BeginTransition(isNight: false);
    private void HandleNightStart(int night) => BeginTransition(isNight: true);

    private void BeginTransition(bool isNight)
    {
        if (_transitionDuration <= 0f)
        {
            ApplyInstant(isNight);
            return;
        }

        _targetIsNight = isNight;
        _blend = 0f;

        if (_seaMaterialInstance != null)
        {
            for (int i = 0; i < _waterColorProperties.Count; i++)
            {
                MaterialColorTransition entry = _waterColorProperties[i];
                if (entry.isValid)
                    entry.fromColor = _seaMaterialInstance.GetColor(entry.propertyId);
            }
        }

        if (_hasMist)
        {
            _fromMistColor = _mistMain.startColor.color;
            _fromMistRate = _mistEmission.rateOverTime.constant;
        }

        enabled = true;
    }

    private void Update()
    {
        _blend += Time.deltaTime / _transitionDuration;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blend));

        if (_seaMaterialInstance != null)
        {
            for (int i = 0; i < _waterColorProperties.Count; i++)
            {
                MaterialColorTransition entry = _waterColorProperties[i];
                if (!entry.isValid) continue;

                Color targetCol = _targetIsNight ? entry.nightColor : entry.dayColor;
                _seaMaterialInstance.SetColor(entry.propertyId, Color.Lerp(entry.fromColor, targetCol, t));
            }
        }

        if (_hasMist)
        {
            Color targetMistCol = _targetIsNight ? _nightMistColor : _dayMistColor;
            float targetMistRate = _targetIsNight ? _nightMistEmissionRate : _dayMistEmissionRate;

            _mistMain.startColor = Color.Lerp(_fromMistColor, targetMistCol, t);
            _mistEmission.rateOverTime = Mathf.Lerp(_fromMistRate, targetMistRate, t);
        }

        if (_blend >= 1f)
            enabled = false;
    }

    private void ApplyInstant(bool isNight)
    {
        _targetIsNight = isNight;

        if (_seaMaterialInstance != null)
        {
            for (int i = 0; i < _waterColorProperties.Count; i++)
            {
                MaterialColorTransition entry = _waterColorProperties[i];
                if (!entry.isValid) continue;

                Color targetCol = isNight ? entry.nightColor : entry.dayColor;
                _seaMaterialInstance.SetColor(entry.propertyId, targetCol);
            }
        }

        if (_hasMist)
        {
            _mistMain.startColor = isNight ? _nightMistColor : _dayMistColor;
            _mistEmission.rateOverTime = isNight ? _nightMistEmissionRate : _dayMistEmissionRate;
        }
    }
}