using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<AudioManager>();
                if (_instance == null)
                {
                    var go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Library")]
    [SerializeField] private AudioLibrary _library;

    [Header("Music Crossfade")]
    [SerializeField, Min(0.05f)] private float _defaultMusicFadeDuration = 0.75f;

    [Header("SFX Pool")]
    [SerializeField, Min(4)] private int _sfxPoolSize = 20;

    private AudioSource _musicSourceA;
    private AudioSource _musicSourceB;
    private AudioSource _ambienceSource;
    private bool _usingSourceA = true;
    private float _currentMusicBaseVolume = 1f;
    private Coroutine _musicFadeRoutine;

    private readonly List<AudioSource> _sfxPool = new List<AudioSource>();
    private int _sfxPoolCursor;

    public AudioLibrary Library => _library;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (_library == null)
            _library = Resources.Load<AudioLibrary>("AudioLibrary");

        InitializeAudioSources();
    }

    private void OnEnable()
    {
        GameAudioSettings.OnChanged += HandleVolumeSettingsChanged;
        ApplyVolumeSettings();
    }

    private void OnDisable()
    {
        GameAudioSettings.OnChanged -= HandleVolumeSettingsChanged;
    }

    private void InitializeAudioSources()
    {
        if (_musicSourceA == null)
        {
            _musicSourceA = CreateChildSource("MusicSource_A", loop: true, spatialBlend: 0f);
            _musicSourceB = CreateChildSource("MusicSource_B", loop: true, spatialBlend: 0f);
        }

        if (_ambienceSource == null)
        {
            _ambienceSource = CreateChildSource("AmbienceSource", loop: true, spatialBlend: 0f);
        }

        if (_sfxPool.Count == 0)
        {
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                _sfxPool.Add(CreateChildSource($"SFXSource_{i:00}", loop: false, spatialBlend: 0f));
            }
        }
    }

    private AudioSource CreateChildSource(string childName, bool loop, float spatialBlend)
    {
        var child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = spatialBlend;
        return src;
    }

    private void HandleVolumeSettingsChanged()
    {
        ApplyVolumeSettings();
    }

    public void ApplyVolumeSettings()
    {
        float musicVol = GameAudioSettings.MusicVolume * _currentMusicBaseVolume;
        AudioSource activeMusic = _usingSourceA ? _musicSourceA : _musicSourceB;
        if (activeMusic != null && _musicFadeRoutine == null)
        {
            activeMusic.volume = musicVol;
        }

        if (_ambienceSource != null && _ambienceSource.isPlaying && _library != null)
        {
            _ambienceSource.volume = GameAudioSettings.SfxVolume * _library.islandAmbienceVolume;
        }
    }

    // =========================================================================
    // MUSIC (BGM) & AMBIENCE
    // =========================================================================
    public void PlayMenuMusic()
    {
        StopAmbience();
        if (_library == null || _library.menuBgm == null) return;
        PlayMusic(_library.menuBgm, _library.menuBgmVolume, _defaultMusicFadeDuration);
    }

    public void PlayDayMusic()
    {
        StartIslandAmbience();
        if (_library == null || _library.dayBgm == null) return;
        PlayMusic(_library.dayBgm, _library.dayBgmVolume, _defaultMusicFadeDuration);
    }

    public void PlayNightMusic()
    {
        StartIslandAmbience();
        if (_library == null || _library.nightBgm == null) return;
        PlayMusic(_library.nightBgm, _library.nightBgmVolume, _defaultMusicFadeDuration);
    }

    public void StartIslandAmbience()
    {
        if (_library == null || _library.islandAmbienceLoop == null || _ambienceSource == null) return;

        if (!_ambienceSource.isPlaying || _ambienceSource.clip != _library.islandAmbienceLoop)
        {
            _ambienceSource.clip = _library.islandAmbienceLoop;
            _ambienceSource.volume = GameAudioSettings.SfxVolume * _library.islandAmbienceVolume;
            _ambienceSource.Play();
        }
    }

    public void StopAmbience()
    {
        if (_ambienceSource != null && _ambienceSource.isPlaying)
        {
            _ambienceSource.Stop();
            _ambienceSource.clip = null;
        }
    }

    public void PlayMusic(AudioClip clip, float baseVolume = 1f, float fadeDuration = -1f)
    {
        if (clip == null) return;

        AudioSource active = _usingSourceA ? _musicSourceA : _musicSourceB;
        if (active != null && active.isPlaying && active.clip == clip)
        {
            _currentMusicBaseVolume = Mathf.Clamp01(baseVolume);
            ApplyVolumeSettings();
            return;
        }

        float fade = fadeDuration >= 0f ? fadeDuration : _defaultMusicFadeDuration;
        _currentMusicBaseVolume = Mathf.Clamp01(baseVolume);

        if (_musicFadeRoutine != null)
            StopCoroutine(_musicFadeRoutine);

        _musicFadeRoutine = StartCoroutine(CrossfadeMusicRoutine(clip, _currentMusicBaseVolume, fade));
    }

    public void StopMusic(float fadeDuration = 0.5f)
    {
        if (_musicFadeRoutine != null)
            StopCoroutine(_musicFadeRoutine);

        _musicFadeRoutine = StartCoroutine(FadeOutAllMusicRoutine(fadeDuration));
    }

    private IEnumerator CrossfadeMusicRoutine(AudioClip nextClip, float targetBaseVolume, float duration)
    {
        AudioSource outgoing = _usingSourceA ? _musicSourceA : _musicSourceB;
        AudioSource incoming = _usingSourceA ? _musicSourceB : _musicSourceA;
        _usingSourceA = !_usingSourceA;

        incoming.clip = nextClip;
        incoming.volume = 0f;
        incoming.Play();

        float startOutVol = outgoing != null && outgoing.isPlaying ? outgoing.volume : 0f;
        float elapsed = 0f;

        if (duration <= 0.01f)
        {
            if (outgoing != null)
            {
                outgoing.Stop();
                outgoing.clip = null;
                outgoing.volume = 0f;
            }
            incoming.volume = GameAudioSettings.MusicVolume * targetBaseVolume;
            _musicFadeRoutine = null;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float masterMusic = GameAudioSettings.MusicVolume;

            if (outgoing != null && outgoing.isPlaying)
                outgoing.volume = Mathf.Lerp(startOutVol, 0f, t);

            incoming.volume = Mathf.Lerp(0f, masterMusic * targetBaseVolume, t);
            yield return null;
        }

        if (outgoing != null)
        {
            outgoing.Stop();
            outgoing.clip = null;
            outgoing.volume = 0f;
        }

        incoming.volume = GameAudioSettings.MusicVolume * targetBaseVolume;
        _musicFadeRoutine = null;
    }

    private IEnumerator FadeOutAllMusicRoutine(float duration)
    {
        float startA = _musicSourceA != null ? _musicSourceA.volume : 0f;
        float startB = _musicSourceB != null ? _musicSourceB.volume : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0.01f ? Mathf.Clamp01(elapsed / duration) : 1f;
            if (_musicSourceA != null) _musicSourceA.volume = Mathf.Lerp(startA, 0f, t);
            if (_musicSourceB != null) _musicSourceB.volume = Mathf.Lerp(startB, 0f, t);
            yield return null;
        }

        if (_musicSourceA != null) { _musicSourceA.Stop(); _musicSourceA.clip = null; }
        if (_musicSourceB != null) { _musicSourceB.Stop(); _musicSourceB.clip = null; }
        _musicFadeRoutine = null;
    }

    // =========================================================================
    // SFX (2D / UI & 3D World)
    // =========================================================================
    public void PlaySfx(SoundCue cue, float volumeMultiplier = 1f)
    {
        if (cue == null || !cue.TryConsumeClip(out AudioClip clip, out float pitch)) return;

        float finalVolume = Mathf.Clamp01(cue.volume * volumeMultiplier * GameAudioSettings.SfxVolume);
        if (finalVolume <= 0.001f) return;

        AudioSource src = AcquireSfxSource();
        src.transform.localPosition = Vector3.zero;
        src.spatialBlend = 0f;
        src.pitch = pitch;
        src.volume = finalVolume;
        src.PlayOneShot(clip, 1f);
    }

    public void PlaySfxAt(SoundCue cue, Vector3 worldPosition, float volumeMultiplier = 1f)
    {
        TryPlaySfxAt(cue, worldPosition, volumeMultiplier);
    }

    private bool TryPlaySfxAt(SoundCue cue, Vector3 worldPosition, float volumeMultiplier = 1f)
    {
        if (cue == null || !cue.TryConsumeClip(out AudioClip clip, out float pitch)) return false;

        float finalVolume = Mathf.Clamp01(cue.volume * volumeMultiplier * GameAudioSettings.SfxVolume);
        if (finalVolume <= 0.001f) return false;

        AudioSource src = AcquireSfxSource();
        src.transform.position = worldPosition;
        src.spatialBlend = 0.65f;
        src.minDistance = 5f;
        src.maxDistance = 60f;
        src.pitch = pitch;
        src.volume = finalVolume;
        src.PlayOneShot(clip, 1f);
        return true;
    }

    private AudioSource AcquireSfxSource()
    {
        if (_sfxPool.Count == 0)
            InitializeAudioSources();

        for (int i = 0; i < _sfxPool.Count; i++)
        {
            int idx = (_sfxPoolCursor + i) % _sfxPool.Count;
            if (!_sfxPool[idx].isPlaying)
            {
                _sfxPoolCursor = (idx + 1) % _sfxPool.Count;
                return _sfxPool[idx];
            }
        }

        AudioSource fallback = _sfxPool[_sfxPoolCursor];
        _sfxPoolCursor = (_sfxPoolCursor + 1) % _sfxPool.Count;
        return fallback;
    }

    // =========================================================================
    // HELPER UI SFX
    // =========================================================================
    public void PlayUiHover() => PlaySfx(_library != null ? _library.uiButtonHover : null);
    public void PlayUiClick() => PlaySfx(_library != null ? _library.uiButtonClick : null);
    public void PlayUiStartGame() => PlaySfx(_library != null ? _library.uiStartGame : null);
    public void PlayUiPopupOpen() => PlaySfx(_library != null ? _library.uiPopupOpen : null);
    public void PlayUiPopupClose() => PlaySfx(_library != null ? _library.uiPopupClose : null);
    public void PlayUiLevelNodeSelect() => PlaySfx(_library != null ? _library.uiLevelNodeSelect : null);
    public void PlayUiSliderTick() => PlaySfx(_library != null ? _library.uiSliderTick : null);
    public void PlayUiActionBlocked() => PlaySfx(_library != null ? _library.uiActionBlocked : null);

    // =========================================================================
    // HELPER PHASE & GRID ISLAND SFX
    // =========================================================================
    public void PlayPhaseDayStart() => PlaySfx(_library != null ? _library.phaseDayStart : null);
    public void PlayPhaseNightStart() => PlaySfx(_library != null ? _library.phaseNightStart : null);
    public void PlayNightCleared() => PlaySfx(_library != null ? _library.nightCleared : null);

    public void PlayTileHover(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.tileHover : null, worldPos);
    public void PlayTileCommandClick(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.tileCommandClick : null, worldPos);
    public void PlayBlockCleaningTick(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.blockCleaningTick : null, worldPos);
    public void PlayBlockCleaned(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.blockCleaned : null, worldPos);

    // =========================================================================
    // HELPER TREE SFX
    // =========================================================================
    public void PlayTreeClick(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.treeClick : null, worldPos);
    public void PlayTreeWorkTick(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.treeWorkTick : null, worldPos);
    public void PlayTreeHealTick(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.treeHealTick : null, worldPos);
    public void PlayTreeStageUp(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.treeStageUp : null, worldPos);
    public void PlayTreeStageDown(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.treeStageDown : null, worldPos);
    public void PlayTreeHitByEnemy(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.treeHitByEnemy : null, worldPos);
    public void PlayTreeDowngradeShot(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.treeDowngradeShot : null, worldPos);

    public void PlayTreeElementAssigned(ElementType element, Vector3 worldPos)
    {
        if (_library == null) return;
        switch (element)
        {
            case ElementType.Fire:
                PlaySfxAt(_library.treeElementFire, worldPos);
                break;
            case ElementType.Water:
                PlaySfxAt(_library.treeElementWater, worldPos);
                break;
            case ElementType.Lightning:
                PlaySfxAt(_library.treeElementLightning, worldPos);
                break;
        }
    }

    public void PlayTreeTurretShoot(ElementType element, Vector3 worldPos)
    {
        if (_library == null) return;
        switch (element)
        {
            case ElementType.Fire:
                PlaySfxAt(_library.treeTurretFire, worldPos);
                break;
            case ElementType.Water:
                PlaySfxAt(_library.treeTurretWater, worldPos);
                break;
            case ElementType.Lightning:
                PlaySfxAt(_library.treeTurretLightning, worldPos);
                break;
        }
    }

    // =========================================================================
    // HELPER SPIRIT SFX
    // =========================================================================
    public void PlaySpiritSelect() => PlaySfx(_library != null ? _library.spiritSelect : null);
    public void PlaySpiritDeselect() => PlaySfx(_library != null ? _library.spiritDeselect : null);
    public void PlaySpiritCommandMove() => PlaySfx(_library != null ? _library.spiritCommandMove : null);
    public void PlaySpiritCommandAttack() => PlaySfx(_library != null ? _library.spiritCommandAttack : null);
    public void PlaySpiritMoveStep(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.spiritMoveStep : null, worldPos);
    public void PlaySpiritProjectileHit(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.spiritProjectileHit : null, worldPos);

    public void PlaySpiritAttack(ElementType element, bool isSpecial, Vector3 worldPos)
    {
        if (_library == null) return;
        if (isSpecial && TryPlaySfxAt(_library.spiritSpecialAttack, worldPos))
            return;

        switch (element)
        {
            case ElementType.Fire:
                PlaySfxAt(_library.spiritAttackFire, worldPos);
                break;
            case ElementType.Water:
                PlaySfxAt(_library.spiritAttackWater, worldPos);
                break;
            case ElementType.Lightning:
                PlaySfxAt(_library.spiritAttackLightning, worldPos);
                break;
            default:
                PlaySfxAt(_library.spiritAttackFire, worldPos);
                break;
        }
    }

    // =========================================================================
    // HELPER ENEMY SFX
    // =========================================================================
    public void PlayEnemyHover(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyHover : null, worldPos);
    public void PlayEnemySpawnWater(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemySpawnWater : null, worldPos);
    public void PlayEnemySpawnAir(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemySpawnAir : null, worldPos);
    public void PlayEnemyIslandJump(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyIslandJump : null, worldPos);
    public void PlayEnemyWormEmerge(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyWormEmerge : null, worldPos);
    public void PlayEnemyWormSubmerge(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyWormSubmerge : null, worldPos);
    public void PlayEnemyAttackMelee(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyAttackMelee : null, worldPos);
    public void PlayEnemyAttackRanged(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyAttackRanged : null, worldPos);
    public void PlayEnemyProjectileHit(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyProjectileHit : null, worldPos);
    public void PlayEnemyHit(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyHit : null, worldPos);
    public void PlayEnemyDeath(Vector3 worldPos) => PlaySfxAt(_library != null ? _library.enemyDeath : null, worldPos);
}