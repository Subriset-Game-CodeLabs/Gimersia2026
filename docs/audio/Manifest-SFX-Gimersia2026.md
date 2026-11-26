# Manifest SFX Terorganisir — Gimersia 2026

Semua aset SFX berada di bawah `Assets/_Project/Audio/SFX/` dan dikelompokkan mengikuti kategori di `AudioLibrary.cs`. Aset **Music / BGM / Ambience** (`Main Menu Bgm`, `Day Bgm`, `Night Bgm`, `Island Ambience Loop`) dikecualikan sesuai instruksi.

## Struktur Folder

```
Assets/_Project/Audio/SFX/
├── 01_UI/        (8 cues: ButtonHover, ButtonClick*, CloseButtonClick, StartGame, PopupOpen, LevelSelect, SliderTick*, ActionBlocked*)
├── 02_Phase/     (3 cues: DayStart, NightStart, NightCleared)
├── 03_Island/    (4 cues: TileHover, TileCommandClick, BlockCleaningTick, BlockCleaned)
├── 04_Trees/     (13 cues: Click, Work, Heal, StageUp, StageDown, Element Fire/Water/Lightning, HitByEnemy, Turret Fire/Water/Lightning, DowngradeShot)
├── 05_Spirits/   (10 cues: Select, Deselect, CommandMove, CommandAttack, MoveStep, Attack Fire/Water/Lightning, SpecialAttack, ProjectileHit)
└── 06_Enemies/   (11 cues: Hover, SpawnWater, SpawnAir, IslandJump, WormEmerge, WormSubmerge, Attack Melee/Ranged, ProjectileHit, Hit, Death)
```

`*` Tiga cue UI tambahan (`uiButtonClick`, `uiSliderTick`, `uiActionBlocked`) ikut disertakan karena aktif direferensikan di `AudioLibrary.cs`.

> **Catatan Unity:** File `.meta` tidak dibuat secara manual agar tidak menimbulkan konflik GUID; Unity Editor akan otomatis men-generate file `.meta` saat project dibuka. Setelah dibuka, klip di masing-masing folder dapat langsung di-assign ke `Assets/_Project/ScriptableObjects/Audio/AudioLibrary.asset`.

## Daftar Lengkap Pemetaan SFX


### 01_UI

| Item SFX | Field `AudioLibrary` | File Terorganisir (`Assets/_Project/Audio/SFX/{cat}/`) | Durasi | Sumber Asli | Lisensi |
|---|---|---|---|---|---|
| sfx Button Hover | `uiButtonHover` | `01_UI/SFX_UI_ButtonHover_01.ogg` | 0.06s | Kenney UI Audio: `rollover2.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Button Hover | `uiButtonHover` | `01_UI/SFX_UI_ButtonHover_02.ogg` | 0.07s | Kenney UI Audio: `rollover3.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Button Hover | `uiButtonHover` | `01_UI/SFX_UI_ButtonHover_03.ogg` | 0.11s | Kenney UI Audio: `rollover4.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Button Click (AudioLibrary extra) | `uiButtonClick` | `01_UI/SFX_UI_ButtonClick_01.ogg` | 0.09s | Kenney UI Audio: `click1.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Button Click (AudioLibrary extra) | `uiButtonClick` | `01_UI/SFX_UI_ButtonClick_02.ogg` | 0.06s | Kenney UI Audio: `click2.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Button Click (AudioLibrary extra) | `uiButtonClick` | `01_UI/SFX_UI_ButtonClick_03.ogg` | 0.09s | Kenney UI Audio: `click3.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Close button Click | `uiPopupClose` | `01_UI/SFX_UI_CloseButtonClick_01.ogg` | 0.06s | Kenney Interface Sounds: `back_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Close button Click | `uiPopupClose` | `01_UI/SFX_UI_CloseButtonClick_02.ogg` | 0.07s | Kenney Interface Sounds: `back_002.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Close button Click | `uiPopupClose` | `01_UI/SFX_UI_CloseButtonClick_03.ogg` | 0.15s | Kenney Interface Sounds: `close_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Start Game button | `uiStartGame` | `01_UI/SFX_UI_StartGame_01.wav` | 1.30s | Mixkit Free Sound Effects: `mixkit_2867_confirmation_tone.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| sfx Start Game button | `uiStartGame` | `01_UI/SFX_UI_StartGame_02.ogg` | 0.54s | Kenney Interface Sounds: `confirmation_002.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Popup button | `uiPopupOpen` | `01_UI/SFX_UI_PopupOpen_01.wav` | 0.38s | Mixkit Free Sound Effects: `mixkit_2357_bubble_popup_alert.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| sfx Popup button | `uiPopupOpen` | `01_UI/SFX_UI_PopupOpen_02.wav` | 0.30s | Mixkit Free Sound Effects: `mixkit_2356_dry_popup_alert.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| sfx Popup button | `uiPopupOpen` | `01_UI/SFX_UI_PopupOpen_03.ogg` | 0.19s | Kenney Interface Sounds: `maximize_007.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Level Select | `uiLevelNodeSelect` | `01_UI/SFX_UI_LevelSelect_01.wav` | 0.14s | Fantasy Sound Effects Library (OGA): `Menu_Select_00.wav` | [CC0 1.0](https://opengameart.org/content/fantasy-sound-effects-library) |
| sfx Level Select | `uiLevelNodeSelect` | `01_UI/SFX_UI_LevelSelect_02.ogg` | 0.29s | Kenney Interface Sounds: `confirmation_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Level Select | `uiLevelNodeSelect` | `01_UI/SFX_UI_LevelSelect_03.ogg` | 0.13s | Kenney Interface Sounds: `glass_002.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Slider Tick (AudioLibrary extra) | `uiSliderTick` | `01_UI/SFX_UI_SliderTick_01.ogg` | 0.05s | Kenney UI Audio: `switch12.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Slider Tick (AudioLibrary extra) | `uiSliderTick` | `01_UI/SFX_UI_SliderTick_02.ogg` | 0.03s | Kenney UI Audio: `switch13.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Slider Tick (AudioLibrary extra) | `uiSliderTick` | `01_UI/SFX_UI_SliderTick_03.ogg` | 0.03s | Kenney UI Audio: `switch14.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| sfx Action Blocked (AudioLibrary extra) | `uiActionBlocked` | `01_UI/SFX_UI_ActionBlocked_01.ogg` | 0.16s | Kenney Interface Sounds: `error_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Action Blocked (AudioLibrary extra) | `uiActionBlocked` | `01_UI/SFX_UI_ActionBlocked_02.ogg` | 0.16s | Kenney Interface Sounds: `error_002.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| sfx Action Blocked (AudioLibrary extra) | `uiActionBlocked` | `01_UI/SFX_UI_ActionBlocked_03.ogg` | 0.10s | Kenney Interface Sounds: `error_004.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |

### 02_Phase

| Item SFX | Field `AudioLibrary` | File Terorganisir (`Assets/_Project/Audio/SFX/{cat}/`) | Durasi | Sumber Asli | Lisensi |
|---|---|---|---|---|---|
| Phase Day Start | `phaseDayStart` | `02_Phase/SFX_Phase_DayStart_01.wav` | 1.68s | Mixkit Free Sound Effects: `mixkit_3109_relaxing_bell_chime.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Phase Day Start | `phaseDayStart` | `02_Phase/SFX_Phase_DayStart_02.wav` | 2.93s | Mixkit Free Sound Effects: `mixkit_938_uplifting_bells_notification.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Phase Night Start | `phaseNightStart` | `02_Phase/SFX_Phase_NightStart_01.wav` | 1.60s | Mixkit Free Sound Effects: `mixkit_987_game_notification_wave_alarm.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Phase Night Start | `phaseNightStart` | `02_Phase/SFX_Phase_NightStart_02.ogg` | 1.42s | 100 CC0 SFX (OGA): `gong_01.ogg` | [CC0 1.0](https://opengameart.org/content/100-cc0-sfx) |
| Night Cleared | `nightCleared` | `02_Phase/SFX_Phase_NightCleared_01.wav` | 3.00s | Mixkit Free Sound Effects: `mixkit_270_fantasy_game_success.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Night Cleared | `nightCleared` | `02_Phase/SFX_Phase_NightCleared_02.wav` | 2.44s | Mixkit Free Sound Effects: `mixkit_2019_ethereal_fairy_win.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |

### 03_Island

| Item SFX | Field `AudioLibrary` | File Terorganisir (`Assets/_Project/Audio/SFX/{cat}/`) | Durasi | Sumber Asli | Lisensi |
|---|---|---|---|---|---|
| Tile Hover | `tileHover` | `03_Island/SFX_Island_TileHover_01.ogg` | 0.02s | Kenney Interface Sounds: `tick_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Tile Hover | `tileHover` | `03_Island/SFX_Island_TileHover_02.ogg` | 0.02s | Kenney Interface Sounds: `tick_002.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Tile Hover | `tileHover` | `03_Island/SFX_Island_TileHover_03.ogg` | 0.05s | Kenney Interface Sounds: `tick_004.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Tile Command Click | `tileCommandClick` | `03_Island/SFX_Island_TileCommandClick_01.ogg` | 0.11s | Kenney Interface Sounds: `drop_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Tile Command Click | `tileCommandClick` | `03_Island/SFX_Island_TileCommandClick_02.ogg` | 0.14s | Kenney Impact Sounds: `impactGeneric_light_000.ogg` | [CC0 1.0](https://kenney.nl/assets/impact-sounds) |
| Tile Command Click | `tileCommandClick` | `03_Island/SFX_Island_TileCommandClick_03.ogg` | 0.12s | Kenney Impact Sounds: `impactGeneric_light_001.ogg` | [CC0 1.0](https://kenney.nl/assets/impact-sounds) |
| Block Cleaning Tick | `blockCleaningTick` | `03_Island/SFX_Island_BlockCleaningTick_01.ogg` | 0.14s | Kenney Interface Sounds: `scratch_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Block Cleaning Tick | `blockCleaningTick` | `03_Island/SFX_Island_BlockCleaningTick_02.ogg` | 0.14s | Kenney Interface Sounds: `scratch_002.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Block Cleaning Tick | `blockCleaningTick` | `03_Island/SFX_Island_BlockCleaningTick_03.ogg` | 0.12s | Kenney Interface Sounds: `scratch_003.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Block Cleaned | `blockCleaned` | `03_Island/SFX_Island_BlockCleaned_01.wav` | 1.30s | Mixkit Free Sound Effects: `mixkit_871_fairy_magic_sparkle.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Block Cleaned | `blockCleaned` | `03_Island/SFX_Island_BlockCleaned_02.wav` | 1.25s | Mixkit Free Sound Effects: `mixkit_3060_sparkle_hybrid_transition.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |

### 04_Trees

| Item SFX | Field `AudioLibrary` | File Terorganisir (`Assets/_Project/Audio/SFX/{cat}/`) | Durasi | Sumber Asli | Lisensi |
|---|---|---|---|---|---|
| Tree Click | `treeClick` | `04_Trees/SFX_Tree_Click_01.wav` | 0.29s | Mixkit Free Sound Effects: `mixkit_2073_game_ball_tap.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Click | `treeClick` | `04_Trees/SFX_Tree_Click_02.ogg` | 0.24s | 80 CC0 RPG SFX (OGA): `wood_01.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Tree Click | `treeClick` | `04_Trees/SFX_Tree_Click_03.ogg` | 0.19s | 80 CC0 RPG SFX (OGA): `wood_02.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Tree Work | `treeWorkTick` | `04_Trees/SFX_Tree_Work_01.wav` | 0.23s | Fantasy Sound Effects Library (OGA): `Footstep_Dirt_00.wav` | [CC0 1.0](https://opengameart.org/content/fantasy-sound-effects-library) |
| Tree Work | `treeWorkTick` | `04_Trees/SFX_Tree_Work_02.wav` | 0.27s | Fantasy Sound Effects Library (OGA): `Footstep_Dirt_01.wav` | [CC0 1.0](https://opengameart.org/content/fantasy-sound-effects-library) |
| Tree Work | `treeWorkTick` | `04_Trees/SFX_Tree_Work_03.wav` | 0.25s | Fantasy Sound Effects Library (OGA): `Footstep_Dirt_04.wav` | [CC0 1.0](https://opengameart.org/content/fantasy-sound-effects-library) |
| Tree Heal | `treeHealTick` | `04_Trees/SFX_Tree_Heal_01.ogg` | 0.25s | 80 CC0 RPG SFX (OGA): `item_gem_01.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Tree Heal | `treeHealTick` | `04_Trees/SFX_Tree_Heal_02.ogg` | 0.20s | 80 CC0 RPG SFX (OGA): `item_gem_02.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Tree Heal | `treeHealTick` | `04_Trees/SFX_Tree_Heal_03.ogg` | 0.29s | 80 CC0 RPG SFX (OGA): `item_gem_03.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Tree Stage Up | `treeStageUp` | `04_Trees/SFX_Tree_StageUp_01.wav` | 1.75s | Mixkit Free Sound Effects: `mixkit_2018_winning_notification.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Stage Up | `treeStageUp` | `04_Trees/SFX_Tree_StageUp_02.wav` | 2.00s | Mixkit Free Sound Effects: `mixkit_601_happy_bell_alert.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Stage Down | `treeStageDown` | `04_Trees/SFX_Tree_StageDown_01.wav` | 1.20s | Mixkit Free Sound Effects: `mixkit_2943_tree_branch_break.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Stage Down | `treeStageDown` | `04_Trees/SFX_Tree_StageDown_02.wav` | 1.18s | Mixkit Free Sound Effects: `mixkit_2945_twig_breaking.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Element Fire | `treeElementFire` | `04_Trees/SFX_Tree_Element_Fire_01.wav` | 2.65s | Mixkit Free Sound Effects: `mixkit_1347_fireball_spell.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Element Fire | `treeElementFire` | `04_Trees/SFX_Tree_Element_Fire_02.wav` | 2.14s | Mixkit Free Sound Effects: `mixkit_1348_fire_woosh.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Element Water | `treeElementWater` | `04_Trees/SFX_Tree_Element_Water_01.wav` | 1.67s | Mixkit Free Sound Effects: `mixkit_2999_magic_bubbles_spell.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Element Water | `treeElementWater` | `04_Trees/SFX_Tree_Element_Water_02.wav` | 2.00s | Mixkit Free Sound Effects: `mixkit_3179_water_drop_splashes.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Element Lightning | `treeElementLightning` | `04_Trees/SFX_Tree_Element_Lightning_01.wav` | 1.77s | 150 Combat & Magic SFX Bundle (CC0): `ElectricExplosion_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Tree Element Lightning | `treeElementLightning` | `04_Trees/SFX_Tree_Element_Lightning_02.wav` | 3.11s | Mixkit Free Sound Effects: `mixkit_2596_electric_whoosh.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Hit By Enemy | `treeHitByEnemy` | `04_Trees/SFX_Tree_HitByEnemy_01.wav` | 0.42s | Mixkit Free Sound Effects: `mixkit_2182_wood_hard_hit.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Hit By Enemy | `treeHitByEnemy` | `04_Trees/SFX_Tree_HitByEnemy_02.ogg` | 0.31s | Kenney Impact Sounds: `impactWood_heavy_000.ogg` | [CC0 1.0](https://kenney.nl/assets/impact-sounds) |
| Tree Hit By Enemy | `treeHitByEnemy` | `04_Trees/SFX_Tree_HitByEnemy_03.ogg` | 0.25s | 75 CC0 Breaking / Falling / Hit SFX (OGA): `bfh1_wood_hit_01.ogg` | [CC0 1.0](https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx) |
| Tree Turret - Fire | `treeTurretFire` | `04_Trees/SFX_Tree_Turret_Fire_01.ogg` | 0.65s | 80 CC0 RPG SFX (OGA): `spell_fire_07.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Tree Turret - Fire | `treeTurretFire` | `04_Trees/SFX_Tree_Turret_Fire_02.ogg` | 1.00s | 80 CC0 RPG SFX (OGA): `spell_fire_06.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Tree Turret - Water | `treeTurretWater` | `04_Trees/SFX_Tree_Turret_Water_01.wav` | 0.42s | Mixkit Free Sound Effects: `mixkit_1317_water_bubble.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Tree Turret - Water | `treeTurretWater` | `04_Trees/SFX_Tree_Turret_Water_02.ogg` | 0.36s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_09.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Tree Turret - Water | `treeTurretWater` | `04_Trees/SFX_Tree_Turret_Water_03.ogg` | 0.32s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_10.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Tree Turret - Lightning | `treeTurretLightning` | `04_Trees/SFX_Tree_Turret_Lightning_01.wav` | 0.34s | 150 Combat & Magic SFX Bundle (CC0): `ElectroZap_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Tree Turret - Lightning | `treeTurretLightning` | `04_Trees/SFX_Tree_Turret_Lightning_02.wav` | 0.37s | 150 Combat & Magic SFX Bundle (CC0): `EnergyZap_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Tree Turret - Lightning | `treeTurretLightning` | `04_Trees/SFX_Tree_Turret_Lightning_03.wav` | 0.57s | 150 Combat & Magic SFX Bundle (CC0): `Retro Electric Short 03_s05.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Tree Downgrade Shot | `treeDowngradeShot` | `04_Trees/SFX_Tree_DowngradeShot_01.wav` | 0.61s | 150 Combat & Magic SFX Bundle (CC0): `BlastImpact_01_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Tree Downgrade Shot | `treeDowngradeShot` | `04_Trees/SFX_Tree_DowngradeShot_02.wav` | 0.65s | 150 Combat & Magic SFX Bundle (CC0): `HeavyShot_01_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |

### 05_Spirits

| Item SFX | Field `AudioLibrary` | File Terorganisir (`Assets/_Project/Audio/SFX/{cat}/`) | Durasi | Sumber Asli | Lisensi |
|---|---|---|---|---|---|
| Spirit Select | `spiritSelect` | `05_Spirits/SFX_Spirit_Select_01.ogg` | 0.12s | Kenney Interface Sounds: `glass_003.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Spirit Select | `spiritSelect` | `05_Spirits/SFX_Spirit_Select_02.ogg` | 0.11s | Kenney Interface Sounds: `glass_005.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Spirit Select | `spiritSelect` | `05_Spirits/SFX_Spirit_Select_03.ogg` | 0.26s | Kenney Interface Sounds: `maximize_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Spirit Deselect | `spiritDeselect` | `05_Spirits/SFX_Spirit_Deselect_01.ogg` | 0.26s | Kenney Interface Sounds: `minimize_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Spirit Deselect | `spiritDeselect` | `05_Spirits/SFX_Spirit_Deselect_02.ogg` | 0.19s | Kenney Interface Sounds: `drop_002.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Spirit Command Move | `spiritCommandMove` | `05_Spirits/SFX_Spirit_CommandMove_01.ogg` | 0.55s | 80 CC0 RPG SFX (OGA): `spell_02.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Spirit Command Move | `spiritCommandMove` | `05_Spirits/SFX_Spirit_CommandMove_02.ogg` | 0.29s | Kenney Interface Sounds: `drop_004.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Spirit Command Attack | `spiritCommandAttack` | `05_Spirits/SFX_Spirit_CommandAttack_01.wav` | 0.37s | RPG Sound Pack (OGA): `sword-unsheathe2.wav` | [CC0 1.0](https://opengameart.org/content/rpg-sound-pack) |
| Spirit Command Attack | `spiritCommandAttack` | `05_Spirits/SFX_Spirit_CommandAttack_02.wav` | 0.42s | RPG Sound Pack (OGA): `sword-unsheathe5.wav` | [CC0 1.0](https://opengameart.org/content/rpg-sound-pack) |
| Spirit Command Attack | `spiritCommandAttack` | `05_Spirits/SFX_Spirit_CommandAttack_03.ogg` | 0.63s | 80 CC0 RPG SFX (OGA): `spell_01.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Spirit Move Step | `spiritMoveStep` | `05_Spirits/SFX_Spirit_MoveStep_01.wav` | 0.15s | RPG Sound Pack (OGA): `swing2.wav` | [CC0 1.0](https://opengameart.org/content/rpg-sound-pack) |
| Spirit Move Step | `spiritMoveStep` | `05_Spirits/SFX_Spirit_MoveStep_02.wav` | 0.14s | RPG Sound Pack (OGA): `swing3.wav` | [CC0 1.0](https://opengameart.org/content/rpg-sound-pack) |
| Spirit Move Step | `spiritMoveStep` | `05_Spirits/SFX_Spirit_MoveStep_03.ogg` | 0.38s | Kenney RPG Audio: `cloth4.ogg` | [CC0 1.0](https://kenney.nl/assets/rpg-audio) |
| Spirit Attack Fire | `spiritAttackFire` | `05_Spirits/SFX_Spirit_Attack_Fire_01.wav` | 1.09s | Mixkit Free Sound Effects: `mixkit_1326_wizard_fire_woosh.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Spirit Attack Fire | `spiritAttackFire` | `05_Spirits/SFX_Spirit_Attack_Fire_02.ogg` | 1.06s | 80 CC0 RPG SFX (OGA): `spell_fire_01.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Spirit Attack Fire | `spiritAttackFire` | `05_Spirits/SFX_Spirit_Attack_Fire_03.ogg` | 1.07s | 80 CC0 RPG SFX (OGA): `spell_fire_02.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Spirit Attack Water | `spiritAttackWater` | `05_Spirits/SFX_Spirit_Attack_Water_01.wav` | 1.05s | Mixkit Free Sound Effects: `mixkit_3000_liquid_bubble.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Spirit Attack Water | `spiritAttackWater` | `05_Spirits/SFX_Spirit_Attack_Water_02.ogg` | 0.56s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_14.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Spirit Attack Water | `spiritAttackWater` | `05_Spirits/SFX_Spirit_Attack_Water_03.ogg` | 0.48s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_15.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Spirit Attack Lightning | `spiritAttackLightning` | `05_Spirits/SFX_Spirit_Attack_Lightning_01.wav` | 0.74s | 150 Combat & Magic SFX Bundle (CC0): `HeavyZap_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Spirit Attack Lightning | `spiritAttackLightning` | `05_Spirits/SFX_Spirit_Attack_Lightning_02.wav` | 0.77s | 150 Combat & Magic SFX Bundle (CC0): `Retro Electric Short 17_s05.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Spirit Attack Lightning | `spiritAttackLightning` | `05_Spirits/SFX_Spirit_Attack_Lightning_03.wav` | 0.92s | Mixkit Free Sound Effects: `mixkit_1670_short_laser_gun_shot.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Spirit Special Attack | `spiritSpecialAttack` | `05_Spirits/SFX_Spirit_SpecialAttack_01.wav` | 2.47s | Mixkit Free Sound Effects: `mixkit_873_light_spell.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Spirit Special Attack | `spiritSpecialAttack` | `05_Spirits/SFX_Spirit_SpecialAttack_02.wav` | 1.56s | 150 Combat & Magic SFX Bundle (CC0): `EMP_01_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Spirit Projectile Hit | `spiritProjectileHit` | `05_Spirits/SFX_Spirit_ProjectileHit_01.wav` | 0.24s | 150 Combat & Magic SFX Bundle (CC0): `Impact_01_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Spirit Projectile Hit | `spiritProjectileHit` | `05_Spirits/SFX_Spirit_ProjectileHit_02.wav` | 0.24s | 150 Combat & Magic SFX Bundle (CC0): `Impact_02_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Spirit Projectile Hit | `spiritProjectileHit` | `05_Spirits/SFX_Spirit_ProjectileHit_03.wav` | 0.27s | 150 Combat & Magic SFX Bundle (CC0): `SmallImpact_01_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |

### 06_Enemies

| Item SFX | Field `AudioLibrary` | File Terorganisir (`Assets/_Project/Audio/SFX/{cat}/`) | Durasi | Sumber Asli | Lisensi |
|---|---|---|---|---|---|
| Enemy Hover | `enemyHover` | `06_Enemies/SFX_Enemy_Hover_01.ogg` | 0.12s | Kenney Interface Sounds: `bong_001.ogg` | [CC0 1.0](https://kenney.nl/assets/interface-sounds) |
| Enemy Hover | `enemyHover` | `06_Enemies/SFX_Enemy_Hover_02.ogg` | 0.17s | Kenney UI Audio: `rollover6.ogg` | [CC0 1.0](https://kenney.nl/assets/ui-audio) |
| Enemy Spawn Water | `enemySpawnWater` | `06_Enemies/SFX_Enemy_SpawnWater_01.ogg` | 0.72s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_02.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Spawn Water | `enemySpawnWater` | `06_Enemies/SFX_Enemy_SpawnWater_02.ogg` | 0.77s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_03.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Spawn Water | `enemySpawnWater` | `06_Enemies/SFX_Enemy_SpawnWater_03.ogg` | 0.79s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_12.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Spawn Air | `enemySpawnAir` | `06_Enemies/SFX_Enemy_SpawnAir_01.wav` | 0.95s | Mixkit Free Sound Effects: `mixkit_2637_fast_air_sweep.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Enemy Spawn Air | `enemySpawnAir` | `06_Enemies/SFX_Enemy_SpawnAir_02.wav` | 1.10s | Mixkit Free Sound Effects: `mixkit_1491_arrow_whoosh.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Enemy Spawn Air | `enemySpawnAir` | `06_Enemies/SFX_Enemy_SpawnAir_03.wav` | 1.33s | Mixkit Free Sound Effects: `mixkit_1492_cinematic_whoosh_fast.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Enemy Island Jump | `enemyIslandJump` | `06_Enemies/SFX_Enemy_IslandJump_01.wav` | 1.32s | Mixkit Free Sound Effects: `mixkit_2043_player_jumping_game.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Enemy Island Jump | `enemyIslandJump` | `06_Enemies/SFX_Enemy_IslandJump_02.ogg` | 0.51s | Kenney Impact Sounds: `impactSoft_heavy_000.ogg` | [CC0 1.0](https://kenney.nl/assets/impact-sounds) |
| Enemy Island Jump | `enemyIslandJump` | `06_Enemies/SFX_Enemy_IslandJump_03.ogg` | 0.57s | Kenney Impact Sounds: `impactSoft_heavy_001.ogg` | [CC0 1.0](https://kenney.nl/assets/impact-sounds) |
| Enemy Worm Emerge | `enemyWormEmerge` | `06_Enemies/SFX_Enemy_WormEmerge_01.wav` | 1.74s | Mixkit Free Sound Effects: `mixkit_1198_sea_water_splash.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Enemy Worm Emerge | `enemyWormEmerge` | `06_Enemies/SFX_Enemy_WormEmerge_02.ogg` | 0.46s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_06.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Worm Emerge | `enemyWormEmerge` | `06_Enemies/SFX_Enemy_WormEmerge_03.ogg` | 0.69s | 40 CC0 Water Splash / Slime SFX (OGA): `splash_08.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Worm Submerge | `enemyWormSubmerge` | `06_Enemies/SFX_Enemy_WormSubmerge_01.wav` | 2.28s | Mixkit Free Sound Effects: `mixkit_1180_jump_into_water.wav` | [Mixkit Sound Effects Free License](https://mixkit.co/free-sound-effects/game/) |
| Enemy Worm Submerge | `enemyWormSubmerge` | `06_Enemies/SFX_Enemy_WormSubmerge_02.ogg` | 0.70s | 40 CC0 Water Splash / Slime SFX (OGA): `bubble_01.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Worm Submerge | `enemyWormSubmerge` | `06_Enemies/SFX_Enemy_WormSubmerge_03.ogg` | 0.55s | 40 CC0 Water Splash / Slime SFX (OGA): `bubble_03.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Attack Melee | `enemyAttackMelee` | `06_Enemies/SFX_Enemy_Attack_Melee_01.wav` | 0.38s | RPG Sound Pack (OGA): `bite-small.wav` | [CC0 1.0](https://opengameart.org/content/rpg-sound-pack) |
| Enemy Attack Melee | `enemyAttackMelee` | `06_Enemies/SFX_Enemy_Attack_Melee_02.wav` | 0.35s | RPG Sound Pack (OGA): `bite-small2.wav` | [CC0 1.0](https://opengameart.org/content/rpg-sound-pack) |
| Enemy Attack Melee | `enemyAttackMelee` | `06_Enemies/SFX_Enemy_Attack_Melee_03.wav` | 0.36s | 150 Combat & Magic SFX Bundle (CC0): `impact_punch_medium_000_48kHz_16bit_0.36s_bd322da3_s02.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Enemy Attack Ranged | `enemyAttackRanged` | `06_Enemies/SFX_Enemy_Attack_Ranged_01.wav` | 0.31s | 150 Combat & Magic SFX Bundle (CC0): `ShootShort_s01.wav` | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| Enemy Attack Ranged | `enemyAttackRanged` | `06_Enemies/SFX_Enemy_Attack_Ranged_02.wav` | 0.28s | RPG Sound Pack (OGA): `slime1.wav` | [CC0 1.0](https://opengameart.org/content/rpg-sound-pack) |
| Enemy Attack Ranged | `enemyAttackRanged` | `06_Enemies/SFX_Enemy_Attack_Ranged_03.wav` | 0.32s | RPG Sound Pack (OGA): `slime2.wav` | [CC0 1.0](https://opengameart.org/content/rpg-sound-pack) |
| Enemy Projectile Hit | `enemyProjectileHit` | `06_Enemies/SFX_Enemy_ProjectileHit_01.ogg` | 0.47s | 40 CC0 Water Splash / Slime SFX (OGA): `slime_01.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Projectile Hit | `enemyProjectileHit` | `06_Enemies/SFX_Enemy_ProjectileHit_02.ogg` | 0.45s | 40 CC0 Water Splash / Slime SFX (OGA): `slime_02.ogg` | [CC0 1.0](https://opengameart.org/content/40-cc0-water-splash-slime-sfx) |
| Enemy Projectile Hit | `enemyProjectileHit` | `06_Enemies/SFX_Enemy_ProjectileHit_03.ogg` | 0.26s | 75 CC0 Breaking / Falling / Hit SFX (OGA): `bfh1_hit_09.ogg` | [CC0 1.0](https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx) |
| Enemy Hit | `enemyHit` | `06_Enemies/SFX_Enemy_Hit_01.ogg` | 0.62s | 80 CC0 RPG SFX (OGA): `creature_hurt_01.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Enemy Hit | `enemyHit` | `06_Enemies/SFX_Enemy_Hit_02.ogg` | 0.68s | 80 CC0 RPG SFX (OGA): `creature_hurt_02.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Enemy Hit | `enemyHit` | `06_Enemies/SFX_Enemy_Hit_03.wav` | 0.32s | Monster Sound Effects 2 (OGA): `monster-8.wav` | [CC0 1.0](https://opengameart.org/content/monster-sound-effects-2) |
| Enemy Death | `enemyDeath` | `06_Enemies/SFX_Enemy_Death_01.ogg` | 1.06s | 80 CC0 RPG SFX (OGA): `creature_die_01.ogg` | [CC0 1.0](https://opengameart.org/content/80-cc0-rpg-sfx) |
| Enemy Death | `enemyDeath` | `06_Enemies/SFX_Enemy_Death_02.wav` | 1.00s | Monster Sound Effects 2 (OGA): `monster-1.wav` | [CC0 1.0](https://opengameart.org/content/monster-sound-effects-2) |
| Enemy Death | `enemyDeath` | `06_Enemies/SFX_Enemy_Death_03.wav` | 0.59s | Monster Sound Effects 2 (OGA): `monster-14.wav` | [CC0 1.0](https://opengameart.org/content/monster-sound-effects-2) |

**Total Ringkasan:** 49 SFX cue (131 file audio, 12.27 MB total).
