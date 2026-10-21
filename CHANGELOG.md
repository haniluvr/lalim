# Changelog

All notable changes to **Lalim** are documented here.

This file follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.  
Dates are in `YYYY-MM-DD` format (Philippine Standard Time, UTC+8).  
For full technical detail on any version, see the linked Detailed Update document.

---

## [Unreleased]

Changes not yet committed to a tagged version.

---

## [0.4.0] — 2026-10-22

> **Full technical detail:** [DETAILED_UPDATES_2026-10-22.md](DETAILED_UPDATES_2026-10-22.md)

Commits: `feat: add Visayas minigames, underwater menu, Bakunawa v2, and relic assets` (`372643b`) + `docs: add Oct 22 detailed updates, changelog, readme, and project docs` (`372d601`)  
Branch: `hym-test`  
~63 files/folders added · 80 modified (47 scripts) · 2 deleted

### Added

#### Underwater Main Menu
- `UnderwaterMenuDirector` — 3D opening swim sequence; player and dugong arrive from off-screen right and settle into idle before UI fades in.
- `LalimTitleAnimator` — staggered bubble-rising animation for individual title letters with sinusoidal sway and float; shared across splash and main menu.
- `UnderwaterMenuMotion` and `UIFloat` — secondary motion for menu actors and gentle UI bobbing.
- `UnderwaterMenu/` asset pack — custom materials (`M_Bubble`, `M_BubbleText`, `M_Fish`, `M_Shaft`, `M_Castle`, `UW_*`), caustics, seabed mesh, post-processing profile, and `LalimTitle.prefab`.
- `MenuPlayerAnimator.controller` — swim-in-and-idle transition controller for menu scene.
- `Models/Castle/castle.fbx` — ruin dressing for the menu scene.

#### Boss Encounter & Bakunawa v2
- **Bakunawa v2 Model**: `Bakunawa_v2.fbx` with its own material and textures (`Models/Bakunawa_v2/`), plus `PF_Bakunawa_v2_Model.prefab` world model.
- `BakunawaEncounterStep` — scripted intro cinematic where sea shakes, diver is held, and Bakunawa spirals up 7 s from the abyss for face-to-face dialogue.
- `Bakunawa.cs` state machine — Coiled, Sentinel, Chasing, Striking, Retreating, Appeased states; sense range scales with fish count in player's net-bag. Implements `ITalkPrelude`.
- `BakunawaBody.cs` — procedural multi-segment body, fin wiggle, and dorsal luminescent trail.
- `ITalkPrelude` — interface for NPCs that play an animation before a conversation (`PlayBeforeTalk`, `Prelude`, `TryGetTalkFocus`).
- `bakunawa.png` — dialogue portrait.

#### Moon Phase Ritual — Minigame 4 (Padre Burgos, Zone 3)
- `MoonPhases` — procedural generator for 8 lunar phase textures, ring columns, and glyphs (no manual art assets needed).
- `MoonCircle` — ritual stone circle component rendering phase glyphs and vertical light columns.
- `MoonPhaseRitualStep` — quest step shuffling 8 ritual circles around Bakunawa's lair; requires visiting in chronological moon phase order (New Moon → Waning Crescent) to pacify the serpent.
- `MoonRitual.asset` — DDA profile configuring phase labels, reshuffling penalties, and Spirit Sense guidance pulses.
- `MAT_MoonCircle_Stone.mat`, `MAT_MoonCircle_Glow.mat` (`Materials/MoonRitual/`).
- `ChallengeType.MoonRitual` in `GameEvents`.

#### Relic Assets & UI Visuals
- **Pearl of Luminescence**: glTF model, PBR textures, optimized mesh (`PearlOfLuminescence_Mesh.asset`), URP material (`M_Relic_Pearl.mat`), UI icon (`icon_relic_pearl.png`), and updated prefab `PF_Relic_PearlOfLuminescence.prefab`.
- **Kataw Gift**: optimized necklace mesh (`KatawGift_Necklace_Mesh.asset`), materials (`M_Relic_KatawGift_Coral/SeaGlass/Shell.mat`), and updated prefab `PF_Relic_KatawGift_PH.prefab`.
- **Dialogue Portraits**: `kataw-male.png`, `kataw-female.png`, `old-kataw-male.png`, `old-kataw-female.png` added to `Sprites/Characters/`.
- **Player & dugong look**: `M_MainPlayer.mat`, `M_Dugong.mat`, `MainPlayer_Albedo.jpg`, `Dugong_Albedo.jpg`.
- **Environment materials**: `MAT_Anemone_Glow.mat`, `M_Skylight_Shaft.mat`, `M_GlowStone_Cyan.mat`, `M_GlowStone_Mint.mat`.

#### Level 2 Mechanics & Quest Steps
- `ReleaseSpot` & `SchoolReleaseSwirl` — trigger system and figure-eight release VFX when guiding rescued fish into correct sanctuaries.
- `EquipRelicStep` — quest step verifying equipped relic; auto-grants required relic in test environments.
- `MoontideGiftSwirl` — visual spiral effect when receiving Moontide from Manunubo.
- `PlanktonByZone` — zone-reactive plankton density and color tinting.
- `WaterBrightness` — centralized contrast utility calculating underwater visibility for shaders and UI.

#### Audio
- Visayas Zone 3 SFX (`Audio/SFX/Visayas/PadreBurgos/`): `SFX_PadreBurgos_BakunawaRoar`, `…Growl`, `…Hiss`, `…Strike`, `…Calm`, `…Swim_1..4`, `SFX_PadreBurgos_RockCollapse`, `LOOP_PadreBurgos_ChaseTension`. Assigned directly on `Bakunawa` (its own `AudioClip` fields, not `SfxCue`s).
- Moon ritual music (`Audio/Music/PadreBurgos/`): `MEL_Moon_1_NewMoon` … `MEL_Moon_8_WaningCrescent`, `MUS_Bakunawa_Lullaby`; assigned on `MoonPhaseRitualStep` in `03_Visayas.unity`.

#### Documentation
- `CONTRIBUTING.md`, `README.md`, `CHANGELOG.md`, `LICENSE`, `DETAILED_UPDATES_2026-10-22.md`.

#### Editor & Debugging Tools
- `Zone3GlowStones` (`Tools ▸ Lalim ▸ Level 2 ▸ Build Zone 3 Glow Stones`) — fluorescent guide stones along Trench B and the abyss.
- `PlayFromZone` (`Tools ▸ Lalim ▸ Play From ▸ Level N · Zone M` / `Restore My Save`) — fast-boot into any zone with a synthetic save; the real save is backed up and restored.
- `KatawGiftPlaceholderBuilder` (`Tools ▸ Lalim ▸ Relics ▸ Build Kataw Gift Placeholder`) — builds the Kataw Gift placeholder prefab.
- `RelicAssetBuilder` gains `Tools ▸ Lalim ▸ Relics ▸ Build Pearl of Luminescence`.
- Code-only builders (no menu item): `Level2KatawVillage.Build(seed)` (Kataw family groups in Ajuy), `Level2Currents.Build(…)` (Moontide current routes), `Level2LevelPieces.Build…()` (Ajuy ledges and lip lantern; Padre Burgos abyss ladders and hide caves).

### Changed
- `SchoolRelocationStep` — capacity-limited net-bag (max 3 schools), diver speed penalty per carried school, Bakunawa aggro scaling, and Compass-guided sanctuary routing.
- `Sanctuary` — capacity tracking, bioluminescent state machine, and Bakunawa proximity darkening.
- `RelocationHud` — release card removed; only the Compass bar remains (fades within ~60 m).
- `SelectiveCatchingStep` — three Kataw lessons (throw, identify, patience) + full test, protected species catch penalty, and Spirit Sense highlighting. `SelectiveCatching.asset` gains a `schoolsToNet` knob.
- `FishGatheringStep` — conch-call and herd-home detection wired to `ManunuboWisp` brightness.
- `PredatorAlertUI` — threat meter with proximity zones and predator-diver hide verification.
- `KatawVillager`, `DugongCompanion`, `StatueDialogueRouter`, `StatueTalkFraming`, `CameraController` — `ITalkPrelude` support, look-at damping, rim-light fade, Bakunawa camera focus, `HoldAt` yaw.
- `TideCurrent` — `StreakMaterial` property, `All` static collection, gentle-push tune.
- `GameConstants.Zones` — Visayas constants renamed to `VISAYAS_CALUYA`, `VISAYAS_AJUY`, `VISAYAS_PADRE_BURGOS` (IDs unchanged).
- `SceneLoader` / `LoadingStages` / `SceneLoadProgressEvent` — loading screens now show what the loader is doing (`Phase` text).
- `NoTapSound` — moved out of `UiTapSound.cs` into its own file; behaviour unchanged.
- `02_MainMenu.unity` rebuilt as a live underwater scene; `01_Splash.unity` uses `LalimTitleAnimator`.
- `03_Visayas.unity` — Bakunawa v2, Kataw village, currents, glow stones, level pieces and audio placed.
- `DugongAnimator` (single `Swim` state), `PlayerAnimator` (`FloatSwim` state).
- `Bakunawa.fbx`, `dugong.fbx`, `Player_final_final.fbx` re-exported; `MAT_Bakunawa_Body.mat` double-sided; `M_WaterSurface.mat` transparent blue.
- `.gitignore` — new dev-artifacts block: `graphify-out/`, `Captures/`, `Assets/Screenshots/`, `Assets/_Recovery/`, `Assets/_AxisTest/`, `.obsidian/`.

### Removed
- `1_sample.md` — scratch file at the repo root.
- `nextsteps (aug 4).md` — old to-do notes.

---

## [0.3.0] — 2026-10-08

> **Full technical detail:** [DETAILED_UPDATES_2026-10-08.md](DETAILED_UPDATES_2026-10-08.md)

Commit: `feat: add Visayas environment, audio, and core mechanics`  
Branch: `hym-test`  
~86 files/folders added · 98 modified · 3 deleted

### Added

#### Audio System (session-wide)
- `MusicDirector` — `DontDestroyOnLoad` singleton; carries BGM seamlessly across all scene loads with 2.5 s crossfades.
- `MusicLibrary` — `ScriptableObject` asset at `Resources/Audio/MusicLibrary`; maps scene names to BGM mode (Track, KeepPrevious, Loading, OwnMusic, Level).
- `ZoneAmbience` — lazy per-level manager; crossfades zone ambient beds in 3 s as the diver moves between zones.
- `Sfx` + `SfxLibrary` — static cue system; `SfxCue` enum maps to clips in `Resources/Audio/SfxLibrary`. Auto-fires on `ChallengeCompletedEvent` / `ChallengeFailedEvent`.
- `AmbientOneShots` — fire-and-forget positional SFX for one-off scene events.
- `AudioLevels` — Music / SFX / Ambient / Voice mixer knob wrappers.
- `UiTapSound` — `DontDestroyOnLoad` singleton; plays `ButtonTap` on any `Button` or `Toggle` automatically.
- Audio asset folders: `Audio/Music/`, `Audio/Ambient/Luzon|Visayas|Shared/`, `Audio/SFX/Luzon|Visayas|Shared/`.
- `MUS_Intro.mp3` — opening BGM (New Game through first Teleport).

#### Swim Stamina Bar & Ability Tuning
- `SwimStaminaPool` — pure-math 6-segment pool; Surge charges it, Riptide spends it.
  - Combo 1 = +⅓ seg, combo 2 = +½ seg total, combo 3 = +1 full seg total.
  - Each segment = 10 s of Riptide.
  - Refills on checkpoint respawn; saved as a 0–1 fill.
- `PauseClock` — unscaled real-time clock that stops while `timeScale == 0`; HUD widgets use this instead of `Time.unscaledDeltaTime`.

#### Inventory & Relic HUD
- `InventoryModalUI` (rewritten) — runtime frosted-glass window; two sections (Relics, Tools); detail panel with Equip/Unequip; pauses world while open.
- `RelicLoadout` — static tracker for one equipped relic, per-relic cooldowns, save/restore.
- `RelicHudButtons` — HUD button for active relics; radial cooldown sweep; key **1** on desktop.
- `RelicIcons` — procedurally drawn icons for Radiant Conch, Woven Net Fragment, Compass (no art required).
- `ItemPortraits` — lazy 256×256 `RenderTexture` portrait of an item's world model.
- Level 2 relic constants: `MOONTIDE`, `KATAW_GIFT`, `PEARL_OF_LUMINESCENCE`.
- Placeholder item data assets and prefabs for all three Level 2 relics.
- Relic icon sprites: `icon_relic_compass.png`, `icon_relic_conch.png`, `icon_relic_net.png`, `relic_button_bg.png`.

#### Level 2 — Visayas (Groundwork)
- World model FBX split into 8 files: terrain, 3 zone models, cave tunnel, 3 trenches (`Models/Environment/Visayas/`).
- **Bakunawa** model (`Models/Bakunawa/Bakunawa.fbx`) + prefab — Level 2's boss sea-serpent.
- **Kataw** merfolk (`Models/Kataw/`): 6 rigged bodies (Male, Female, ChildMale, ChildFemale, OldMale, OldFemale) + animations + prefab folder.
- `Manunubo.png` dialogue portrait.
- `Visayas/` materials folder.
- `PF_Wanderer_WhaleShark` prefab.
- `Shark.fbx` and `WhaleShark/` FBX folder.
- **Minigame 1 — Fish Gathering** (`Challenges/Gathering/`): `FishGatheringStep`, `Herd`, `HerdRibbon`, `ConchCall`, `HidingZone`, `Predator`, `PredatorAlertUI`.
- **Minigame 2 — Selective Catching** (`Challenges/Catching/`): `SelectiveCatchingStep`, `CatchableSchool`, `NetBag`, `NetThrower`, `RootPool`.
- **Minigame 3 — School Relocation** (`Challenges/Relocation/`): `SchoolRelocationStep`, `Bakunawa`, `BakunawaBody`, `Sanctuary`, `RelocationHud`.
- `Level2DialogueScripts` — placeholder dialogue for all Level 2 spirits and teachers.
- DDA profile assets: `FishGathering.asset`, `SchoolRelocation.asset`, `SelectiveCatching.asset`.
- `Level2DifficultyKnobs` — knob name constants.
- `03_Visayas.unity` scene (placeholder).

#### Level Template Builder
- `LevelTemplateBuilder` editor tool (**Tools ▸ Lalim ▸ Level Template ▸**):
  - **Build Template Prefabs** — saves per-manager `Core/` prefabs + `PF_ZoneTemplate` / `PF_ZoneTemplate_Final`.
  - **Create Template Scene** — `Scenes/Levels/_Template/00_LevelTemplate.unity`.
  - **Add Missing Level Pieces** — surgical add to existing scenes.
  - **Fix Up Level IDs** — stamps zone/step/checkpoint IDs from the region prefix (required after duplicating the template).

#### Creature Animation Pipeline
- `CreatureRig` — `PlayableGraph`-based swim/idle clip player; random start time prevents lockstep animation.
- `SeaCreatureClips` (`Resources/SeaCreatureClips.asset`) — model name → clip registry.
- `RiggedSeaCreatures` editor tool — builds `SeaCreatureClips`, swaps Luzon static meshes, updates wanderer prefabs.
- `AmbientSwimmers` — drives Visayas baked-in sea life by name fragment; only cameras-near creatures move.
- `CruisingAnimal` — wide elliptical loops for large animals (whale sharks).

#### Diver & NPC Feel
- `DiverIdleFloat` — 0.4 s settle-in; bob 0.18 m at 0.8 Hz; sway 3°; upright lean 72°. Model only, never collider.
- `TwoBoneIK` — analytic two-bone IK for Generic rigs; runs in `LateUpdate`.
- `KatawVillager` — idle bob, watch-diver turn, amber quest-giver rim light, proximity hum.
- `ManunuboWisp` — drift/hover spirit; brightness controlled by gathering minigame progress.

#### Tide Currents
- `TideCurrent` — path-based tube of moving water. Invisible pre-Moontide; glows as moonlit streaks and carries the diver at 9 m/s post-Moontide. Gentle-push mode (0.5–1 m/s) works without Moontide.

#### Minigame UX
- `CalloutUI` — non-blocking frosted-pill speaker callout at top of screen; queued, same-speaker dedup.
- `HintBulb` — shared hint component; card-attach and standalone modes; badge counts remaining hints.
- `SortingStation` — Visayas sorting variant placeholder.

#### Scene & Rendering
- `TransitionScreen` — live-canvas crossfade replaces screenshot-based captures; no GPU readback.
- `FollowCameraInBounds` — clamps ambient particle emitters to the camera inside a zone volume.

#### Editor Tools
- `SolidSceneryColliders` — adds mesh colliders to solid scenery by name prefix; undo-able.
- `Level1ZoneSites` — geometry-aware sampler for zone 2's `ZoneSiteShuffle` pool.
- `ZoneSiteShuffle` — per-playthrough minigame area randomisation; draw seeded from save (reload = same layout).

### Changed
- **`SurgeAbility`**: now owns `SwimStaminaPool`; 7 s cooldown only triggers after the 3rd surge in a combo.
- **`RiptideAbility`**: reads `SurgeAbility.Swim` for segment count; drains bar live.
- **`GameConstants.Abilities`**: `SWIM_STAMINA_SEGMENTS = 6`, `RIPTIDE_SECONDS_PER_SEGMENT = 10`, `TIDEBOND_COOLDOWN` changed from **30 s → 7 s**.
- **`GameConstants.Relics`**: added `MOONTIDE`, `KATAW_GIFT`, `PEARL_OF_LUMINESCENCE`.
- **`SaveData`**: added `equippedRelicIds` field.
- **`GameEvents`**: added `RelicEquipChangedEvent`, `RelicUsedEvent`, `RelicHoldEvent`.
- **`SceneLoader`**: uses `TransitionScreen` instead of screenshot crossfades.
- **`ProximityMusic` / `GameAudio`**: hooked into `MusicDirector` for level takeover and handback.
- **`LevelFlowDirector` / `ZoneQuest` / `MinigameStage`**: `ZoneSiteShuffle.ApplyAll()` called before player placement; `ZoneAmbience.Play` on zone change.
- **`ItemData`**: added `worldModelPrefab` field (for `ItemPortraits`) and `icon` sprite field.
- **`MarineSchool`**: uses `CreatureRig.Play` for fish animation.
- **Existing minigames** (all): `Sfx.Play` calls added (Correct, Wrong, ItemPickup, AltarRise).
- **`AbilityConfig.asset`**: `swimStaminaSegments = 6`, `riptideSecondsPerSegment = 10`, `tidebondCooldown = 7`.
- **Wanderer prefabs** (StingRay, Turtle): `CreatureRig` added.
- **`GameHUD.prefab` / `Player_final_final.prefab`**: `DiverIdleFloat` added; `RelicHudButtons` wired.
- **Level 1 relic item assets**: `worldModelPrefab` and `icon` linked.

### Removed
- `Scripts/UI/CodexModalUI.cs` — deferred; replaced conceptually by `InventoryModalUI`.
- `Scripts/UI/MapModalUI.cs` — deferred; no replacement yet.
- `Sprites/Logos/koral_labs.png` — old studio logo; replaced by `black_cat.png`.

### Fixed
- HUD animations no longer advance while the pause menu is open (`PauseClock`).
- Scene crossfades no longer capture wrong-size or wrong-alpha screenshots (`TransitionScreen`).

---

## [0.2.0] — 2026-10-04

> **Full technical detail:** [DETAILED_UPDATES_2026-10-04.md](DETAILED_UPDATES_2026-10-04.md)

Commit: `complete level 1`  
Branch: `hym-test`  
545 files added · 76 modified · 21 deleted

### Added

#### Skill Kit & HUD
- **Surge** (basic): 8 m dash, chainable 3× as a combo; 7 s cooldown.
- **Riptide** (basic): 1.75× speed for 60 s; hold to activate/cancel.
- **Spirit Sense**: costs 1 stamina orb; `SpiritWisp` leads to objective; `SpiritGlow` highlights targets.
- **Tidebond**: ride the dugong companion at 2.25×; 30 s cooldown.
- **Seagrass Mend**: heals 30 HP over 3 s; 40 s cooldown; starts at 0% each level.
- **Bubble Barrier**: 20 s toxic + hit shield; charges from swimming and other skills; starts half full.
- `StaminaPool` — pure-math 4-orb stamina (unit-tested).
- `PlayerHealth` — HP bar; heals only via Mend or checkpoint respawn.
- `DugongHealth` — green bar under dugong card; 300 HP.
- `HudPalette` — shared seashell colours across HUD elements.
- HUD: landscape-only layout; HP center-bottom; skills right; joystick left; `AbilityButtonUI` radial cooldown overlays.
- `AbilityConfig.asset` — all timing, cost, and range values in one asset.
- Behind-the-back third-person `CameraController` with spring arm.
- `PauseModalUI` — Resume / Save / Exit; slot picker for unsaved games.
- `SettingsModalUI` — Audio / Quality / Controls / System tabs; pearl-knob sliders; shared with main menu.
- `HudHandedness` / `PillSwitch` — right-handed mode mirrors the HUD.
- `GameplayClock` — stops vitals and skill timers during dialogue.

#### Level 1 — Luzon (Full Build)
- **Zone 1 (Amansinaya)**: trash collection (3rd person) + trash sorting (1st person).
- **Zone 2 (Haik)**: water readings × 3 areas + coral site prep and planting × 3 areas.
- **Zone 3 (Neguno)**: ghost-net untangling × 3 areas.
- `LevelFlowDirector` — drives zones in order; handles resume from checkpoint.
- `ZoneQuest` — per-zone statue, toxic water, music, ordered steps.
- Step types: `TalkStep`, `TrashCollectionStep`, `SegregationStep`, `AreaMinigameStep`, `DialogueStep`, `GrantItemStep`, `RelicRewardStep`, `CleanseZoneStep`, `UnlockBarrierStep`, `LevelExitStep`.
- `AreaStation` + `IAreaMinigame` + `MinigameStage` — multi-area first-person minigame framework.
- `ObjectiveTracker` — single "where next" target; read by Spirit Sense and `ProximityMusic`.
- `AchievementService` — Clean Sweep, Steady Hands, Net Breaker, and others.
- `PollutionDirector` — level visually cleans up zone by zone.
- `ToxicWaterVolume` + `ToxicWaterTracker` — 0.5 HP/s drain; HP floored at 1 during first-person minigames.
- `Checkpoint` + `CheckpointManager` — respawn and refill everything.
- DDA: `MinigameDifficultyProfile` assets, `Level1DifficultyKnobs`, `ExplorationMetricsTracker`, `ChallengeManager`.
- Autosave on zone cleanse and level exit.

#### Dialogue System
- `DialogueManager` + `DialogueHUD` — NPC portrait, Story Review, Auto/Speed/Pause buttons.
- `ReviewOverlayUI` — full-screen dark log; NPC lines left, player lines right.
- 3/4 dialogue camera: diver + dugong at statue's TalkPoint; fades in/out.

#### Scene Flow
- Boot → Splash → Main Menu → Name Input → Tutorial → Teleport Hub → Level → Teleport…
- Loading screen with staged loading (memory → scene → shader warm-up via `GraphicsStateCollection`).
- Game scenes crossfade into/out of loading (no black frames).
- Teleport hub with arch portals: `PortalSwirl_Loop.mp4` + seamless looping; locked (grey, fogged) until unlocked.

#### Relics (Level 1)
- Radiant Conch (`00_RadiantConch.asset`), Compass (`01_Compass.asset`), Woven Net Fragment (`02_WovenNetFragment.asset`).
- `RelicRewardStep` + `RelicPresentation` — gift dialogue, item materialises.

#### Minigame Details
- **Trash sorting**: shrines rise from sand; first-person; drag trash to category bowl.
- **Water readings**: frosted SEA SENSORS panel; arc gauges (pH, Salinity, Temp); tap STOP in the green band.
- **Coral site prep**: top-down view; swipe algae, tap dead corals, plant Coral Seed; reef blooms.
- **Ghost-net untangling**: top-down; tap nodes on a square mesh net; free tangled animals.

#### Editor Tools (new in this release)
- **Level 1 ▸ Build Level Flow** — idempotent scene builder.
- **Level 1 ▸ Reset Level 1 Progress**
- **Level 1 ▸ Scatter Zone 1 Trash**
- **Level 1 ▸ Set Up Statue Talk Framing**
- **Level 1 ▸ Drape Ghost Nets**
- **Abilities ▸ Build Ability HUD**
- **Build Reusable Level Prefabs**
- **UI ▸ Use UI Prefabs In Open Scene**
- Reusable `Prefabs/LevelFlow/` prefabs: `PF_LevelSystems`, `PF_Zone`, stations, `PF_FoggyBarrier`, `PF_DialogueSystem`, `PF_QuestObjectiveUI`, `PF_ToastSystem`.

#### Underwater Look
- Post-processing volume, sky dome, depth-texture fog.
- Bubble + plankton VFX, fish schools (`MarineSchool`, `WanderingMarineAnimal`), god rays.
- Pitch-black deep well (Zone 3).

#### Tests
- 98 EditMode tests under `Lalim.Tests`; all passing.
- `StaminaPool` unit tests (pure-math).

### Changed
- `02_Luzon` (old tutorial scene) → `Tutorial/01_Tutorial`.
- `02_LuzonNew` → `Levels/01_Luzon/02_Luzon` (final Level 1).
- `TeleportScene` moved to `Scenes/Others/TeleportScene/`.
- `MoonTideAbility.cs` removed — its concept folded into Bubble Barrier.
- Old UI: `PauseMenu.cs`, `SettingsUI.cs`, `InventoryUI.cs`, `ToolEquipSystem.cs` removed.
- Old placeholder items deleted; `09_CoralSeed` added.

---

## [0.1.0] — 2026-09-18

Commit: `a few tweaks` (`8e8f8d5`)  
Branch: `hym-test`

Baseline commit. URP migration from BiRP complete. Early HUD work (landscape touch controls, codex modal, map modal, `MoonTideAbility` stub). Core systems skeleton in place.

---

## [Pre-history] — 2026-05 → 2026-09

Early prototype commits on `main` and `prototype-test`:

| Commit | Description |
|---|---|
| `5b8895a` | Initialize project: item database, player prefab, input manager, core scenes |
| `5f732b6` | Boot sequence, placeholder Main Menu |
| `5069ed9` | Main menu + first waste-segregation prototype |
| `145a5d1` | Waste segregation challenge complete |
| `136660e` | Add low-poly ocean pack + Luzon scene challenges |
| `0ace2a1` | Challenge setup guide; scene, lighting, animation assets |
| `120a7ba` | Waste segregation challenge integrated |
| `27a0422` | Improved segregation UI; added inventory |
| `6cf9a45` | Coral replanting + water sampling challenges; editor setup utilities |
| `43089fb` | Recovered `02_Luzon` scene |
| `bf98a0f` | UI updates |
| `f9ab4ff` | Landscape touch controls HUD, URP config, codex modal, map modal, MoonTide stub |
| `1a5bfd1` | Merge remote-tracking branch |

---

[Unreleased]: https://github.com/Ombrei/Lalim_Capstone_Final/compare/372d601...hym-test
[0.4.0]: https://github.com/Ombrei/Lalim_Capstone_Final/compare/e35e93a...372d601
[0.3.0]: https://github.com/Ombrei/Lalim_Capstone_Final/compare/c16e4fe...e35e93a
[0.2.0]: https://github.com/Ombrei/Lalim_Capstone_Final/compare/8e8f8d5...c16e4fe
[0.1.0]: https://github.com/Ombrei/Lalim_Capstone_Final/compare/16142e7...8e8f8d5
