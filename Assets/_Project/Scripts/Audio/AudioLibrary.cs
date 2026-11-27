using UnityEngine;

[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Game/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Header("1. BGM - Menu & Gameplay")]
    public AudioClip menuBgm;
    [Range(0f, 1f)] public float menuBgmVolume = 0.75f;

    public AudioClip dayBgm;
    [Range(0f, 1f)] public float dayBgmVolume = 0.70f;

    public AudioClip nightBgm;
    [Range(0f, 1f)] public float nightBgmVolume = 0.75f;

    [Tooltip("Opsional: Suara ombak laut & angin pulau yang diputar pelan selama gameplay.")]
    public AudioClip islandAmbienceLoop;
    [Range(0f, 1f)] public float islandAmbienceVolume = 0.30f;

    [Header("2. SFX - UI & Menu")]
    public SoundCue uiButtonHover = new SoundCue { volume = 0.55f, pitchRange = new Vector2(0.98f, 1.04f), cooldown = 0.04f };
    public SoundCue uiButtonClick = new SoundCue { volume = 0.85f, pitchRange = new Vector2(0.96f, 1.04f) };
    public SoundCue uiStartGame = new SoundCue { volume = 0.95f, pitchRange = new Vector2(1f, 1f) };
    public SoundCue uiPopupOpen = new SoundCue { volume = 0.8f, pitchRange = new Vector2(0.98f, 1.02f) };
    public SoundCue uiPopupClose = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.98f, 1.02f) };
    public SoundCue uiLevelNodeSelect = new SoundCue { volume = 0.85f, pitchRange = new Vector2(0.95f, 1.05f) };
    public SoundCue uiSliderTick = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.98f, 1.05f), cooldown = 0.08f };
    public SoundCue uiActionBlocked = new SoundCue { volume = 0.65f, pitchRange = new Vector2(0.95f, 1.02f), cooldown = 0.12f };

    [Header("3. SFX - Fase Siang, Malam & Wave")]
    public SoundCue phaseDayStart = new SoundCue { volume = 0.85f, pitchRange = new Vector2(0.98f, 1.02f) };
    public SoundCue phaseNightStart = new SoundCue { volume = 0.90f, pitchRange = new Vector2(0.98f, 1.02f) };
    public SoundCue nightCleared = new SoundCue { volume = 0.90f, pitchRange = new Vector2(1f, 1f) };

    [Header("4. SFX - Grid Island & Pembersihan Blok")]
    public SoundCue tileHover = new SoundCue { volume = 0.45f, pitchRange = new Vector2(0.95f, 1.08f), cooldown = 0.06f };
    public SoundCue tileCommandClick = new SoundCue { volume = 0.80f, pitchRange = new Vector2(0.94f, 1.06f), cooldown = 0.05f };
    public SoundCue blockCleaningTick = new SoundCue { volume = 0.65f, pitchRange = new Vector2(0.92f, 1.08f), cooldown = 0.65f };
    public SoundCue blockCleaned = new SoundCue { volume = 0.95f, pitchRange = new Vector2(0.97f, 1.03f) };

    [Header("5. SFX - Pohon (Pertumbuhan, Elemen, Heal & Pertahanan)")]
    public SoundCue treeClick = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.95f, 1.05f), cooldown = 0.06f };
    public SoundCue treeWorkTick = new SoundCue { volume = 0.65f, pitchRange = new Vector2(0.94f, 1.08f), cooldown = 0.65f };
    public SoundCue treeHealTick = new SoundCue { volume = 0.60f, pitchRange = new Vector2(0.96f, 1.06f), cooldown = 0.65f };
    public SoundCue treeStageUp = new SoundCue { volume = 0.95f, pitchRange = new Vector2(0.98f, 1.03f) };
    public SoundCue treeElementFire = new SoundCue { volume = 0.95f, pitchRange = new Vector2(0.98f, 1.02f) };
    public SoundCue treeElementWater = new SoundCue { volume = 0.95f, pitchRange = new Vector2(0.98f, 1.02f) };
    public SoundCue treeElementLightning = new SoundCue { volume = 0.95f, pitchRange = new Vector2(0.98f, 1.02f) };
    public SoundCue treeHitByEnemy = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.90f, 1.10f), cooldown = 0.16f };
    public SoundCue treeStageDown = new SoundCue { volume = 0.95f, pitchRange = new Vector2(0.96f, 1.02f) };
    public SoundCue treeTurretFire = new SoundCue { volume = 0.70f, pitchRange = new Vector2(0.94f, 1.06f), cooldown = 0.12f };
    public SoundCue treeTurretWater = new SoundCue { volume = 0.70f, pitchRange = new Vector2(0.94f, 1.06f), cooldown = 0.12f };
    public SoundCue treeTurretLightning = new SoundCue { volume = 0.70f, pitchRange = new Vector2(0.94f, 1.06f), cooldown = 0.12f };
    public SoundCue treeDowngradeShot = new SoundCue { volume = 0.90f, pitchRange = new Vector2(0.97f, 1.03f) };

    [Header("6. SFX - Spirit (Seleksi, Perintah, Gerak & Serangan)")]
    public SoundCue spiritSelect = new SoundCue { volume = 0.80f, pitchRange = new Vector2(0.96f, 1.06f), cooldown = 0.06f };
    public SoundCue spiritDeselect = new SoundCue { volume = 0.65f, pitchRange = new Vector2(0.96f, 1.02f), cooldown = 0.06f };
    public SoundCue spiritCommandMove = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.95f, 1.06f), cooldown = 0.08f };
    public SoundCue spiritCommandAttack = new SoundCue { volume = 0.85f, pitchRange = new Vector2(0.96f, 1.04f), cooldown = 0.08f };
    public SoundCue spiritMoveStep = new SoundCue { volume = 0.35f, pitchRange = new Vector2(0.90f, 1.12f), cooldown = 0.36f };
    public SoundCue spiritAttackFire = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.94f, 1.08f), cooldown = 0.08f };
    public SoundCue spiritAttackWater = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.94f, 1.08f), cooldown = 0.08f };
    public SoundCue spiritAttackLightning = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.94f, 1.08f), cooldown = 0.08f };
    public SoundCue spiritSpecialAttack = new SoundCue { volume = 0.95f, pitchRange = new Vector2(0.97f, 1.03f), cooldown = 0.10f };
    public SoundCue spiritProjectileHit = new SoundCue { volume = 0.70f, pitchRange = new Vector2(0.92f, 1.10f), cooldown = 0.08f };

    [Header("7. SFX - Enemy (Spawn, Gerak, Serangan, Hit & Kematian)")]
    public SoundCue enemyHover = new SoundCue { volume = 0.40f, pitchRange = new Vector2(0.96f, 1.06f), cooldown = 0.08f };
    public SoundCue enemySpawnWater = new SoundCue { volume = 0.65f, pitchRange = new Vector2(0.92f, 1.08f), cooldown = 0.18f };
    public SoundCue enemySpawnAir = new SoundCue { volume = 0.65f, pitchRange = new Vector2(0.92f, 1.08f), cooldown = 0.18f };
    public SoundCue enemyIslandJump = new SoundCue { volume = 0.80f, pitchRange = new Vector2(0.94f, 1.06f), cooldown = 0.15f };
    public SoundCue enemyWormEmerge = new SoundCue { volume = 0.85f, pitchRange = new Vector2(0.93f, 1.05f), cooldown = 0.20f };
    public SoundCue enemyWormSubmerge = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.93f, 1.05f), cooldown = 0.20f };
    public SoundCue enemyAttackMelee = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.92f, 1.08f), cooldown = 0.12f };
    public SoundCue enemyAttackRanged = new SoundCue { volume = 0.75f, pitchRange = new Vector2(0.94f, 1.08f), cooldown = 0.12f };
    public SoundCue enemyProjectileHit = new SoundCue { volume = 0.65f, pitchRange = new Vector2(0.92f, 1.08f), cooldown = 0.12f };
    public SoundCue enemyHit = new SoundCue { volume = 0.65f, pitchRange = new Vector2(0.90f, 1.12f), cooldown = 0.14f };
    public SoundCue enemyDeath = new SoundCue { volume = 0.85f, pitchRange = new Vector2(0.92f, 1.06f), cooldown = 0.10f };
}