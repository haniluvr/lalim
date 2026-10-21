# DETAILED UPDATES — 2026-10-22

**Branch:** `hym-test`
**Last commit (baseline):** `e35e93a` "feat: add Visayas environment, audio, and core mechanics" (2026-10-08)
**Covers:** every uncommitted change in the working tree from 2026-10-08 to 2026-10-22, in the order the work happened.
**Size (not counting `.meta` files):** ~67 files/folders added · 79 modified (47 scripts: 3,793 insertions, 437 deletions) · 2 deleted.

This file is for teammates and for AI assistants working on Lalim. It explains **what changed, why, where it lives, and how to work with it safely**. Read §1 and §2 before touching anything.

---

## Table of contents

1. [TL;DR](#1-tldr)
2. [⚠️ Read before you pull / commit](#2-️-read-before-you-pull--commit)
3. [Quick start](#3-quick-start)
4. [Timeline — earliest to latest](#4-timeline--earliest-to-latest)
5. [System guide (how the pieces fit)](#5-system-guide)
6. [Editor tools (Tools ▸ Lalim menu)](#6-editor-tools)
7. [Tests](#7-tests)
8. [File inventory — added / modified / deleted](#8-file-inventory)
9. [Rules & conventions for AI assistants and teammates](#9-rules--conventions)
10. [Known issues & open questions](#10-known-issues--open-questions)
11. [Footnotes](#11-footnotes)

---

## 1. TL;DR

- **Underwater main menu — fully built:**
  - `UnderwaterMenuDirector`: the player and dugong swim in from off-screen right and settle into position; the menu UI fades in after they arrive.
  - `LalimTitleAnimator`: the "LALIM" letters rise on a semi-arc one by one like bubbles drifting up, then float gently. Shared by splash screen and main menu.
  - `UnderwaterMenu/` folder: the menu scripts plus all art, materials (`M_Bubble`, `M_BubbleText`, `M_Fish`, `M_Shaft`, `M_Castle`, `UW_*`), `MenuPlayerAnimator.controller`, textures, `SeabedMesh.asset`, caustics, `UW_PostProfile`. `LalimTitle.prefab` is the standalone title rig.
- **Bakunawa v2 — new rigged model:** `Bakunawa_v2.fbx` + `Materials/` + `Textures/` in `Models/Bakunawa_v2/`. The v1 model stays for reference. `PF_Bakunawa_v2_Model.prefab` is the new world-model prefab.
- **Pearl of Luminescence & Kataw Gift — full models & materials:**
  - `PearlOfLuminescence.gltf` (+ `.bin`) + PBR maps in `Models/relics/Pearl/`. `M_Relic_Pearl.mat` for URP. `PearlOfLuminescence_Mesh.asset` optimised.
  - `KatawGift_Necklace_Mesh.asset` + `M_Relic_KatawGift_Coral/SeaGlass/Shell.mat`. `icon_relic_pearl.png` UI icon.
  - `PF_Relic_PearlOfLuminescence.prefab` and `PF_Relic_KatawGift_PH.prefab` updated with real meshes and materials.
- **Kataw & Bakunawa dialogue portraits:** `kataw-male.png`, `kataw-female.png`, `old-kataw-male.png`, `old-kataw-female.png`, `bakunawa.png` in `Sprites/Characters/`.
- **Moon Phase Ritual — Minigame 4 (Padre Burgos, Zone 3):**
  - `MoonPhases` (static class): 8 moon phases with names, colours, and procedurally generated glyph/ring/column textures — no art needed.
  - `MoonCircle`: a single ritual stone circle that draws its phase glyph and column of light.
  - `MoonPhaseRitualStep`: places 8 shuffled circles around Bakunawa's lair; the diver must stand in each in moon order (New → Waning Crescent).[^moonritual] Wrong circle: all darken, Bakunawa stirs (doesn't strike), restart from New Moon. All 8 correct: lullaby plays, Bakunawa appeased. Spirit Sense guides to the next correct circle. DDA knobs: phase labels, reshuffle on restart, free pulse after N restarts.
  - Audio: 8 phase melodies (`MEL_Moon_1_NewMoon` … `MEL_Moon_8_WaningCrescent`) + `MUS_Bakunawa_Lullaby` in `Audio/Music/PadreBurgos/`, all assigned in `03_Visayas.unity`.
  - `MoonRitual.asset` DDA profile; stone/glow materials in `Materials/MoonRitual/`.
- **Bakunawa — fully implemented (AI + scripted encounter):**
  - `BakunawaEncounterStep` (new): scripted first meeting — sea shakes, diver held, Bakunawa rises 7 s from the abyss in a spiral, face-to-face. Talk opens; camera looks up at Bakunawa.
  - `Bakunawa.cs` (+876/−26): full state machine — Coiled, Sentinel, Chasing, Striking, Retreating, Appeased. Sense range scales with schools in net-bag. Implements `ITalkPrelude`. Its sounds are its own `AudioClip` fields (not `SfxCue`s), assigned in the scene.
  - `BakunawaBody.cs` (+360/−15): procedural multi-segment body, fin wiggle, dorsal trail.
- **School Relocation — fully implemented:**
  - `SchoolRelocationStep.cs` (+307/−102): capacity-limited net-bag (3 max), 0.75× diver speed penalty per school, 12 m Bakunawa sense range per school in bag, Compass relic points to nearest sanctuary, wrong sanctuary re-scatters the school.
  - `ReleaseSpot` (new): interact trigger that rides with the diver; "Release fish" prompt when in the right sanctuary.
  - `SchoolReleaseSwirl` (new): released fish circle diver + dugong in a figure-eight then swim into the sanctuary.
  - `Sanctuary.cs` (+261/−23): capacity tracking, glow state machine, Bakunawa proximity darkening.
  - `RelocationHud.cs` (+16/−69): the release card is gone; only the Compass bar remains.
- **EquipRelicStep (new quest step):** waits for the player to equip a specific relic. Grants it first if a test save skipped the level that rewards it. Used to gate Manunubo's "now use it" moment.
- **Selective Catching — fully implemented:**
  - `SelectiveCatchingStep.cs` (+199/−49): 3 Kataw lessons (Amihan: throw, Lakan: identify, Inang Sela: patience) + full test. Protected-species penalty. Spirit Sense colours targets (green/red).
  - `NetBag.cs` (+102/−5), `CatchableSchool.cs` (+25/−6): capacity and wrong-catch penalty UI. New `schoolsToNet` knob in `SelectiveCatching.asset`.
- **Fish Gathering — fully implemented:**
  - `FishGatheringStep.cs` (+36/−7): conch-call and herd-home detection fully wired; `ManunuboWisp` brightness coupled to fraction of herds home.
  - `PredatorAlertUI.cs` (+157/−19): full threat bar, approach zones, hide-confirmation state machine.
- **Character polish:**
  - `DugongCompanion.cs` (+91/−20): idle float sync; `HoldAt` optional yaw.
  - `KatawVillager.cs` (+210/−1): look-at damping, rim-light fade, dialogue callbacks, `ITalkPrelude` support.
  - `StatueDialogueRouter.cs` (+124/−0): full `ITalkPrelude` support; Bakunawa camera override; portrait lookup from new sprites.
  - `ITalkPrelude` (new interface): `PlayBeforeTalk`, `Prelude(talkPoint)`, `TryGetTalkFocus(out focus)`.
  - Animators: `DugongAnimator` swim states merged into one `Swim` state; `PlayerAnimator` gains a `FloatSwim` state.
- **Rendering & ambient polish:**
  - `WaterBrightness` (new): reads `RenderSettings.fogColor` luminance → 0–1 Contrast. Used to swap additive ↔ alpha-blended materials so glows stay readable in cleansed water.
  - `PlanktonByZone` (new): drives the camera plankton through 3 named bands (Caluya: bright motes, Ajuy: brown silt, Padre Burgos: marine snow); eases between bands in 2.5 s.
  - `MoontideGiftSwirl` (new): 3 trail ribbons swirl in a figure-eight around diver and dugong for 8 s when Moontide is gifted.
  - `TideCurrent.cs` (+128/−6): `StreakMaterial` property, `All` static collection.
  - New materials: `M_Dugong.mat`, `M_MainPlayer.mat`, `M_Skylight_Shaft.mat`, `MAT_Anemone_Glow.mat`, `M_GlowStone_Cyan/Mint.mat`, relic and MoonRitual materials. `M_WaterSurface.mat` (Davis3D) is now transparent blue; `MAT_Bakunawa_Body.mat` is double-sided.
  - Player and dugong albedo textures added; `Player_final_final.prefab` and `PF_Dugong.prefab` updated. `Bakunawa.fbx`, `dugong.fbx` and `Player_final_final.fbx` re-exported.
- **Padre Burgos audio:** 12 SFX files (`LOOP_PadreBurgos_ChaseTension.mp3`, `SFX_PadreBurgos_Bakunawa{Calm,Growl,Hiss,Roar,Strike,Swim_1–4}.{wav,mp3}`, `SFX_PadreBurgos_RockCollapse.mp3`) + 9 ritual music clips (see Moon Phase Ritual above).
- **Loading text:** `SceneLoadProgressEvent` gains a `Phase` string; `SceneLoader` and `LoadingStages.Label()` report what the loader is doing ("Freeing up memory…", "Compiling shaders…").
- **`NoTapSound`** moved into its own file `Scripts/UI/NoTapSound.cs` (it used to be declared inside `UiTapSound.cs`). Place it on a panel's parent to silence `UiTapSound` for all its children.
- **Visayas zone constants renamed** in `GameConstants.Zones`: `VISAYAS_CALUYA`, `VISAYAS_AJUY`, `VISAYAS_PADRE_BURGOS` (IDs unchanged: `visayas_zone_a/b/c`).
- **`02_MainMenu.unity` rebuilt** as a live underwater scene with `UnderwaterMenuDirector`.
- **`03_Visayas.unity`** updated: Bakunawa_v2 placed, Kataw village populated, glow stones placed, tide-current routes threaded, level pieces built, ritual and Bakunawa audio assigned.
- **6 new Level 2 editor scripts:** `Zone3GlowStones`, `PlayFromZone` and `KatawGiftPlaceholderBuilder` have menu items; `Level2Currents`, `Level2KatawVillage` and `Level2LevelPieces` are code-only builders (no menu item, see §6). `RelicAssetBuilder` gains **Build Pearl of Luminescence**.
- **Root clean-up:** `1_sample.md` and `nextsteps (aug 4).md` deleted.

---

## 2. ⚠️ Read before you pull / commit

| # | Issue | What to do |
|---|---|---|
| 1 | **`03_Visayas.unity` has not been play-tested end to end** — zone 3 steps are still being wired. | Use **Tools ▸ Lalim ▸ Play From ▸ Level 2 · Zone N** — it guards your real save.[^playfromzone] |
| 2 | **No Padre Burgos zone BGM** — `Audio/Music/PadreBurgos/` holds only the 8 ritual melodies and the lullaby. | Assign a BGM clip to `MusicLibrary` for the Padre Burgos zone before playtesting. |
| 3 | **All new work is untracked**, including `Materials/MoonRitual/`, `Audio/Music/PadreBurgos/` and `Audio/SFX/Visayas/PadreBurgos/`. `03_Visayas.unity` references them. | Commit them together with the scene, or the references break on other machines. |
| 4 | **`graphify-out/`** (repo root, and the older `Assets/Lalim/Scripts/Core/graphify-out/`) is a dev-tool dump. | Add `graphify-out/` to `.gitignore`. Do not commit. |
| 5 | **`Assets/Screenshots/`** has 29 dev screenshots. | Do not commit. Add to `.gitignore` or delete before push. |
| 6 | **`.obsidian/`** at root was created by the Obsidian vault setup. | Add to `.gitignore`, or commit intentionally if the team shares vault config. |
| 7 | **`Captures/`** at root has screen captures. | Do not commit. Add to `.gitignore`. |
| 8 | **`RelicAssetBuilder` / `KatawGiftPlaceholderBuilder`** rebuild relic prefabs in place. | **Relics ▸ Build Pearl of Luminescence** overwrites `PF_Relic_PearlOfLuminescence.prefab`; **Relics ▸ Build Kataw Gift Placeholder** overwrites `PF_Relic_KatawGift_PH.prefab`. Back up manual prefab edits first. |
| 9 | **`PlayFromZone`** backs up the slot-0 save and restores it on Play stop. | If Unity crashes mid-test, run **Tools ▸ Lalim ▸ Play From ▸ Restore My Save** immediately. |
| 10 | **`Level2Currents`, `Level2KatawVillage`, `Level2LevelPieces` have no menu items.** | Call their static `Build…()` methods from an editor script or the Unity MCP bridge (§6). |
| 11 | **`1_sample.md` and `nextsteps (aug 4).md` are deleted** from the working tree. | Confirm nobody still needs them before committing the deletion. |
| 12 | All baseline warnings from the earlier docs still apply: local MCP package path, crash-recovery scenes, stray assets, embedded URP edit, Git LFS, save-file location. | See [DETAILED_UPDATES_2026-10-04.md](DETAILED_UPDATES_2026-10-04.md) §2 and [DETAILED_UPDATES_2026-10-08.md](DETAILED_UPDATES_2026-10-08.md) §2. |

---

## 3. Quick start

**Full flow:** `Scenes/00_BootSequence/00_BootSequence.unity` → Play.
- Boot → Splash (title floats in) → Main Menu (underwater swim-in) → Level 1.

**Test a zone safely:**
- **Tools ▸ Lalim ▸ Play From ▸ Level 1 · Zone 1/2/3** or **Level 2 · Zone 1/2/3** — real save is preserved.

**Run tests:** Window ▸ General ▸ Test Runner ▸ EditMode, filter `Lalim.Tests`.[^tests]

**Dev hotkeys:**

| Key | Action |
|---|---|
| F9 | Ability / vitals debug |
| F10 | Infinite stamina |
| F11 | Cycle difficulty |
| I | Inventory modal |
| 1 | Equipped relic |

---

## 4. Timeline — earliest to latest

> Dates are approximate. File paths are under `Assets/Lalim/` unless noted.

### 4.1 Underwater main menu (2026-10-09 → 10-10)

The main menu was rebuilt as a live 3D underwater environment. Its scripts live in `UnderwaterMenu/`, not `Scripts/`.

- **`UnderwaterMenuDirector`**: player and dugong swim in from off-screen right along smooth paths; dugong turns to its end yaw once settled; menu canvas fades in after arrival. Scene holds end pose in edit mode.
- **`LalimTitleAnimator`**: "LALIM" letters rise staggered 0.28 s apart on a semi-arc, then float gently. Driven by `UnderwaterMenuDirector.Tick(t)` in the menu; runs standalone (`_autoPlay = true`) on the splash screen.
- **`UnderwaterMenuMotion`**: per-actor secondary motion — fin wiggle, body sway, camera drift, parallax.
- **`UIFloat`**: makes any UI element bob gently.
- **Art (all in `UnderwaterMenu/`)**: `SeabedMesh.asset` (procedural floor), `Caustics.png`, `T_Bubble/Fish/Shaft.png`, `Castle_Albedo.jpg`, `M_Bubble/BubbleText/BubbleText_Splash/Fish/Shaft/Castle.mat`, `UW_*.mat` (coral, kelp, rock, ruin, sand; more variants in `UnderwaterMenu/Materials/`), `UW_PostProfile.asset`.
- **`Models/Castle/castle.fbx`**: abyss-wall ruin dressing for the menu (a different file from Level 1's `Models/Environment/Castle/Castle.fbx`).
- **`MenuPlayerAnimator.controller`**: swim animator for the menu player model.
- **`LalimTitle.prefab`**: standalone title rig.
- `02_MainMenu.unity` rebuilt; `01_Splash.unity` updated with `LalimTitleAnimator._autoPlay = true`.

### 4.2 Relic models & materials (2026-10-10 → 10-12)

- **Pearl of Luminescence**: `PearlOfLuminescence.gltf` + `.bin` in `Models/relics/Pearl/` with BaseColor, MetallicRoughness, Normal maps. `PearlOfLuminescence_Mesh.asset` (optimised) + packed `PearlOfLuminescence_MetallicSmoothness.png`. `M_Relic_Pearl.mat` (URP Lit).
- **Kataw Gift**: `KatawGift_Necklace_Mesh.asset` + three materials (Coral, SeaGlass, Shell).
- **`icon_relic_pearl.png`** added to `Sprites/UI/Game Mechanics/` and linked as the icon of `Data/Items/12_PearlOfLuminescence.asset`.
- `PF_Relic_PearlOfLuminescence.prefab` and `PF_Relic_KatawGift_PH.prefab` updated with real meshes and materials.
- **`RelicAssetBuilder.cs`** (+136/−0) gains **Tools ▸ Lalim ▸ Relics ▸ Build Pearl of Luminescence**: optimises the Pearl glTF mesh, builds `M_Relic_Pearl` and rebuilds `PF_Relic_PearlOfLuminescence` in place.
- **`KatawGiftPlaceholderBuilder.cs`** (new, **Tools ▸ Lalim ▸ Relics ▸ Build Kataw Gift Placeholder**): builds the necklace placeholder into `PF_Relic_KatawGift_PH`.

### 4.3 Player & dugong materials (2026-10-12)

- `MainPlayer_Albedo.jpg` added to `Models/MC/` (player was rendering white — albedo was missing).
- `Dugong_Albedo.jpg` added to `Models/Dugong/`.
- `M_MainPlayer.mat` and `M_Dugong.mat` created as URP Lit materials.
- `Player_final_final.fbx` (61.9 MB → 2.2 MB) and `dugong.fbx` (61.8 MB → 0.6 MB) re-exported; `Player_final_final.prefab` and `PF_Dugong.prefab` updated.
- `DugongAnimator.controller`: `SwimForward` / `SwimReverse` replaced by one `Swim` state. `PlayerAnimator.controller`: `FloatSwim` state added.

### 4.4 Kataw portraits & dialogue routing (2026-10-12 → 10-13)

- **Portrait sprites**: `kataw-male/female.png`, `old-kataw-male/female.png`, `bakunawa.png` in `Sprites/Characters/`.
- **`StatueDialogueRouter.cs`** (+124/−0): portrait lookup from new sprites; `ITalkPrelude` delegation before fade-in; `ClearShot()` after dialogue; `KatawVillager.OnDialogueEnd` signal.
- **`ITalkPrelude`** (new interface in `Scripts/Mechanics/`): `PlayBeforeTalk`, `Prelude(talkPoint)`, `TryGetTalkFocus(out focus)`. `Bakunawa.cs` implements it.
- `PF_DialogueSystem.prefab` updated; `Level2DialogueScripts.cs` expanded (+97/−19) with Kataw teacher lessons and Bakunawa's pre-ritual monologue.

### 4.5 Bakunawa v2 & full AI (2026-10-13 → 10-16)

- **`Bakunawa_v2.fbx`** in `Models/Bakunawa_v2/` with `Materials/MAT_Bakunawa_v2.mat` and `Textures/Bakunawa_BaseColor.png`, `Bakunawa_Normal.png`. `PF_Bakunawa_v2_Model.prefab` is the new world-model prefab. The v1 `Bakunawa.fbx` was also re-exported and `MAT_Bakunawa_Body.mat` made double-sided.
- **`Bakunawa.cs`** (+876/−26): full state machine:
  - **Coiled**: in lair. Rises on `BakunawaEncounterStep` trigger.
  - **Sentinel**: patrols; sense range = base + 12 m × schools in bag.
  - **Chasing**: closes to diver; screenshake increases; roar SFX.
  - **Striking**: lunge + rock-collapse particles if diver doesn't hide in time.
  - **Retreating**: backs off; body recoils.
  - **Appeased**: circles slowly, settles with calm SFX. Implements `ITalkPrelude`.
- **`BakunawaBody.cs`** (+360/−15): procedural N-segment body; configurable lag; fin wiggle; dorsal trail renderer; scale ramp on coil.

### 4.6 BakunawaEncounterStep & Padre Burgos audio (2026-10-16 → 10-17)

- **`BakunawaEncounterStep`** (new): scripted first meeting:
  1. Diver crosses world-X at Trench B mouth.
  2. Sea shakes (screenshake + silt particles + falling rock models).
  3. `PlayerController.LockMovement` holds diver in place.
  4. `Bakunawa.RiseToFace()` — 7 s spiral rise.
  5. Talk prompt appears; camera looks up at Bakunawa's face.
  6. Conversation ends → `SchoolRelocationStep` starts.
- `Audio/SFX/Visayas/PadreBurgos/` — 12 SFX files added (see §8.2).
- Bakunawa's sounds are **serialized `AudioClip` fields on `Bakunawa.cs`** (`_roarClip`, `_hissClip`, `_strikeClip`, `_swimClips[]`, `_growlClip`, `_calmClip`, `_chaseLoop`, `_sleepHum`), set through `EditorSetup` / `EditorSetChaseAudio` / `EditorSetSleepHum` and assigned in `03_Visayas.unity`. They don't go through `SfxCue` / `SfxLibrary`; no new `SfxCue` entries were added.

### 4.7 School Relocation — full implementation (2026-10-17 → 10-18)

- **`SchoolRelocationStep.cs`** (+307/−102):
  - Schools scatter on Bakunawa's roar.
  - Net-bag holds ≤ 3 schools; each adds 0.75× speed penalty.
  - Bakunawa sense range: base + 12 m × schools in bag.
  - `Compass` relic (if equipped) points to nearest correct sanctuary (60 m range).
  - Wrong sanctuary: `Sfx.Wrong` + re-scatter. Correct: `SchoolReleaseSwirl.Play()`.
  - After all schools home: conversation resumes → `MoonPhaseRitualStep` begins.
- **`ReleaseSpot`** (new): `SphereCollider` + `IInteractable` that rides with the diver.
- **`SchoolReleaseSwirl`** (new): fish circle diver + dugong in a figure-eight 5 s then swim off. Crabs/lobsters walk (crabs sideways). Phase glyph material fades under the circle.
- **`Sanctuary.cs`** (+261/−23): capacity, glow states (dark → lit → overlit-wrong), Bakunawa proximity darkening.
- **`RelocationHud.cs`** (+16/−69): the release card (one button per school) was removed. Only the Compass bar remains: it shows the nearest unvisited sanctuary's name and distance and fades out within ~60 m.

### 4.8 Moon Phase Ritual — Minigame 4 (2026-10-18 → 10-20)

- **`MoonPhases`** (new, `Challenges/MoonRitual/`):
  - 8 phases: New Moon → Waning Crescent, each with a distinct colour.[^moonritual]
  - Procedural `Texture2D` per phase: the lit crescent shape. Also `Ring` and `Column` textures. No art files required.
- **`MoonCircle`** (new): one stone circle. Draws phase glyph + column of light. `Stand()` pulses colour + plays melody. `Darken()` dims + shakes. `Finale()` flare burst.
- **`MoonPhaseRitualStep`** (new): places 8 `MoonCircle`s at `_ringRadius` around `_centre` (the lair). Shuffles phases. Diver holds still `_holdSeconds` in a circle to activate it. Correct order 0→7: proceed. Wrong: darken all, Bakunawa stirs (doesn't strike), restart. Spirit Sense guides to the next correct circle. DDA: phase labels, reshuffle on restart, free pulse after N restarts.
- **Audio** (`Audio/Music/PadreBurgos/`): `MEL_Moon_1_NewMoon` … `MEL_Moon_8_WaningCrescent` fill `_melodies[0..7]`; `MUS_Bakunawa_Lullaby` fills `_lullaby`. All assigned in `03_Visayas.unity`.
- **Materials**: `Materials/MoonRitual/MAT_MoonCircle_Stone.mat`, `MAT_MoonCircle_Glow.mat`.
- `MoonRitual.asset` DDA profile; `Level2DifficultyKnobs.cs` updated (+6 lines); `ChallengeType.MoonRitual` added to `GameEvents.cs`.

### 4.9 Selective Catching — full implementation (2026-10-20 → 10-21)

- **`SelectiveCatchingStep.cs`** (+199/−49): 3 lessons (Amihan: throw, Lakan: identify, Inang Sela: patience) + full test. Spirit Sense colours targets green/red. Protected-species penalty docks count. Completion → Kataw Gift relic granted.
- `NetBag.cs` (+102/−5): capacity limit, wrong-catch penalty UI.
- `CatchableSchool.cs` (+25/−6): protected-species ID.
- `Data/Difficulty/SelectiveCatching.asset`: new `schoolsToNet` knob (easy 8 / normal 14 / hard 25).

### 4.10 Visual polish & ambient systems (2026-10-21 → 10-22)

- **`PlanktonByZone`** (new): 3 bands (Caluya: sparse bright motes, Ajuy: dense brown silt, Padre Burgos: marine snow). Blends between bands over 2.5 s as player moves east.
- **`WaterBrightness`** (new): `RenderSettings.fogColor` luminance → 0–1 `Contrast`. `ShadeOf(material)` returns a cached alpha-blended twin of an additive material for bright water.
- **`MoontideGiftSwirl`** (new): 3 `TrailRenderer` ribbons figure-eight around diver + dugong for 8 s on Moontide gift. Adapts to `WaterBrightness.Contrast`.
- `TideCurrent.cs` (+128/−6): `StreakMaterial` property exposed, `All` static collection, gentle-push tune.
- New materials: `MAT_Anemone_Glow.mat`, `M_GlowStone_Cyan/Mint.mat`, `M_Skylight_Shaft.mat`. `Davis3D/.../M_WaterSurface.mat` switched to transparent, tinted blue (alpha 0.75).
- `03_Visayas.unity`: glow stones placed, plankton bands configured, skylight shafts added.

### 4.11 Level 2 editor tools (2026-10-21)

- **`Level2Currents`** (code-only, no menu item — `Level2Currents.Build(carrySpeed, radius, streaks)`): threads Moontide current routes through open water (3 m grid search, path smoothed, nodes laid). Covers Ajuy trench, village loops, teaching pools, Padre Burgos gate, abyss climb, abyss floor loop. Respects minimum spacing from existing currents.
- **`Level2KatawVillage`** (code-only — `Level2KatawVillage.Build(seed)`): places Kataw family groups in Ajuy's root village, clear of teachers and obstacles.
- **`Level2LevelPieces`** (code-only — `BuildThrowingLedges()`, `BuildLipLantern()`, `BuildWallLedges(seed)`, `BuildWallCaves()`): Ajuy throwing ledges beside Root Pools and the amber lantern at the Trench B lip; Padre Burgos abyss ladders (soft-coral ledges every 18 m, 3 zig-zag runs) + 2 hide caves with `HidingZone` inside each.
- **`PlayFromZone`** (**Tools ▸ Lalim ▸ Play From ▸ …**): backs up slot-0 save, writes a synthetic completed save to that zone, enters Play, restores real save on Play stop. **Restore My Save** for crash recovery.[^playfromzone]
- **`Zone3GlowStones`** (**Tools ▸ Lalim ▸ Level 2 ▸ Build Zone 3 Glow Stones**): fluorescent pebble clusters along Trench B, abyss walls, and abyss floor (avoids ritual ring and lair).
- **`StatueTalkFraming.cs`** (+86/−0): Bakunawa `TryGetTalkFocus` camera override.

### 4.12 Housekeeping (2026-10-08 → 10-22)

- **`NoTapSound`** moved out of `UiTapSound.cs` (−4 lines) into its own file `Scripts/UI/NoTapSound.cs`. Behaviour unchanged.
- **`GameConstants.Zones`**: `VISAYAS_FISHING_GROUNDS / GHOST_NET_FIELD / RESCUE_SITE` renamed to `VISAYAS_CALUYA / AJUY / PADRE_BURGOS`. The string IDs (`visayas_zone_a/b/c`) are unchanged, so saves are unaffected.
- **Loading text**: `SceneLoadProgressEvent.Phase` (new field); `SceneLoader` (+8/−6) publishes it and `LoadingStages.Label(stage)` (+12) gives each stage a player-facing line.
- **Fonts**: `PlusJakartaSans-ExtraBold/Medium SDF Padded` and `Spicy Sale SDF` atlases regenerated.
- **Deleted** from the repo root: `1_sample.md` (scratch file flagged in the Oct 8 doc) and `nextsteps (aug 4).md` (old to-do notes).

---

## 5. System guide

### 5.1 Underwater main menu

```
02_MainMenu.unity
  UnderwaterMenuDirector ──► LalimTitleAnimator (title rig)
                         ──► Player model (MenuPlayerAnimator)
                         ──► Dugong model
                         ──► UnderwaterMenuMotion (secondary motion)
  UIFloat (menu canvas bob)
  UW_PostProfile (URP atmosphere)
  SeabedMesh (procedural floor)
```

`LalimTitleAnimator` is driven by `UnderwaterMenuDirector.Tick(t)` in the menu; it self-drives from `OnEnable` on the splash screen.

### 5.2 Level 2 quest flow (Visayas)

**Zone 1 — Caluya (Manunubo)**
1. `TalkStep` — intro
2. `EquipRelicStep` (Radiant Conch)
3. `FishGatheringStep`
4. `RelicRewardStep` (Moontide gifted; `MoontideGiftSwirl` plays)
5. `CleanseZoneStep` → `UnlockBarrierStep`

**Zone 2 — Ajuy (Kataw Elder)**
1. `TalkStep` — Elder intro
2. `SelectiveCatchingStep` (3 lessons + test)
3. `RelicRewardStep` (Kataw Gift)
4. `CleanseZoneStep` → `UnlockBarrierStep`

**Zone 3 — Padre Burgos (Bakunawa)**
1. `BakunawaEncounterStep` — dramatic first meeting
2. `SchoolRelocationStep` — carry schools to sanctuaries
3. `MoonPhaseRitualStep` — 8-circle moon ritual
4. `RelicRewardStep` (Pearl of Luminescence)
5. `CleanseZoneStep` → `UnlockBarrierStep` → `LevelExitStep`

### 5.3 Bakunawa's state machine

```
Coiled (lair)
  → BakunawaEncounterStep: RiseToFace
Risen (face-to-face, ITalkPrelude conversation)
  → conversation ends: SchoolRelocationStep starts
Sentinel (patrols; sense = base + 12m × schools in bag)
  → diver detected
Chasing → Strike (if not hidden in time) → Retreat → Sentinel
  → all schools home
Appeased (circles, settles; lullaby plays)
```

### 5.4 Moon Phase Ritual

```
MoonPhaseRitualStep
  spawns 8 MoonCircle (shuffled phases) around _centre
  ← diver holds still _holdSeconds in a circle
     correct order → pulse + melody (_melodies[phase])
     wrong → Darken all, Bakunawa stirs, restart from New Moon
  all 8 correct → Finale × 8, _lullaby, step complete
```

Spirit Sense targets the next correct circle (same orb cost, keeps it from being spammed).

### 5.5 WaterBrightness

```
WaterBrightness.Contrast (0 = dark, 1 = bright)
  └─ reads RenderSettings.fogColor luminance
     SmoothStep(0, 1, InverseLerp(0.25, 0.45, lum))

Users:
  MoontideGiftSwirl  → additive ↔ alpha-blended trail material
  PredatorAlertUI    → threat bar adapts
  PlanktonByZone     → particle colour adapts
```

### 5.6 PlanktonByZone

Bands sorted by `fromX`. Active band = last band where `fromX ≤ player.position.x`. `Update()` lerps `ParticleSystem.emission.rateOverTime`, `main.startColor`, `main.startSize`, `main.gravityModifier` toward the target band over `_blendSeconds`.

---

## 6. Editor tools

New tools (run in the scene noted):

| Menu | What it does |
|---|---|
| **Tools ▸ Lalim ▸ Level 2 ▸ Build Zone 3 Glow Stones** | Fluorescent pebble clusters in Trench B and the abyss. Run in `03_Visayas`. |
| **Tools ▸ Lalim ▸ Play From ▸ Level 1 · Zone 1–3 / Level 2 · Zone 1–3** | Safe zone-skip test entry; real save backed up and restored.[^playfromzone] |
| **Tools ▸ Lalim ▸ Play From ▸ Restore My Save** | Manual fallback if Unity crashed during a Play From test. |
| **Tools ▸ Lalim ▸ Relics ▸ Build Pearl of Luminescence** | Optimised Pearl mesh, `M_Relic_Pearl`, and rebuilds `PF_Relic_PearlOfLuminescence` in place. |
| **Tools ▸ Lalim ▸ Relics ▸ Build Kataw Gift Placeholder** | Rebuilds `PF_Relic_KatawGift_PH` with the necklace placeholder. |

**Code-only builders (no menu item yet):** call these from an editor script or the Unity MCP bridge with `03_Visayas` open.

| Class | Entry points | What it does |
|---|---|---|
| `Level2Currents` | `Build(carrySpeed, radius, streaks, only = null)` | Threads Moontide current routes through Visayas (Ajuy, Trench B, Padre Burgos). |
| `Level2KatawVillage` | `Build(seed)` | Places Kataw family groups in Ajuy's root village. |
| `Level2LevelPieces` | `BuildThrowingLedges()`, `BuildLipLantern()`, `BuildWallLedges(seed)`, `BuildWallCaves()` | Ajuy throwing ledges and lip lantern; Padre Burgos abyss ladders and hide caves. |

All tools from the Oct 4 and Oct 8 docs remain and are unchanged.

---

## 7. Tests

Run: **Window ▸ General ▸ Test Runner ▸ EditMode**, filter to `Lalim.Tests`.[^tests]

The existing 98 EditMode tests all still apply. No new test files were added in this update.

**New pure-math classes that need tests (not yet written):**
- `MoonPhases` — glyph generation, phase colour lookup, order correctness.
- `WaterBrightness.Contrast` — luminance formula at the 0.25 and 0.45 boundaries.
- `PlanktonByZone` — correct band selection and blend rate.
- `SchoolReleaseSwirl` — figure-eight path math, done callback timing.

The Oct 8 list (`SwimStaminaPool`, `RelicLoadout`, `ZoneSiteShuffle`) is still untested too.

---

## 8. File inventory

> `.meta` files are omitted. Paths are relative to `Assets/Lalim/` unless they start with `Assets/` or the repo root.

### 8.1 Added — scripts (new, untracked)

**Challenges/MoonRitual/:** `MoonCircle.cs`, `MoonPhases.cs`

**Challenges/Relocation/:** `ReleaseSpot.cs`, `SchoolReleaseSwirl.cs`

**Mechanics/:** `ITalkPrelude.cs`, `MoontideGiftSwirl.cs`, `PlanktonByZone.cs`, `WaterBrightness.cs`

**Mechanics/Quest/Steps/:** `BakunawaEncounterStep.cs`, `EquipRelicStep.cs`, `MoonPhaseRitualStep.cs`

**UI/:** `NoTapSound.cs` (moved out of `UiTapSound.cs`, not a new class)

**Editor/:** `KatawGiftPlaceholderBuilder.cs`, `Level2Currents.cs`, `Level2KatawVillage.cs`, `Level2LevelPieces.cs`, `PlayFromZone.cs`, `Zone3GlowStones.cs`

**UnderwaterMenu/** (outside `Scripts/`): `LalimTitleAnimator.cs`, `UnderwaterMenuDirector.cs`, `UnderwaterMenuMotion.cs`, `UIFloat.cs`

### 8.2 Added — assets (new, untracked)

| Group | Contents |
|---|---|
| **Audio** | `Audio/SFX/Visayas/PadreBurgos/`: `LOOP_PadreBurgos_ChaseTension.mp3`, `SFX_PadreBurgos_BakunawaCalm.wav`, `…Growl.mp3`, `…Hiss.wav`, `…Roar.mp3`, `…Strike.wav`, `…Swim_1–4.mp3`, `SFX_PadreBurgos_RockCollapse.mp3`; `Audio/Music/PadreBurgos/`: `MEL_Moon_1_NewMoon` … `MEL_Moon_8_WaningCrescent.mp3` (8), `MUS_Bakunawa_Lullaby.mp3` |
| **Data** | `Data/Difficulty/MoonRitual.asset` |
| **Materials** | `Materials/Environment/MAT_Anemone_Glow.mat`; `M_Dugong.mat`, `M_MainPlayer.mat`, `M_Skylight_Shaft.mat`; `MoonRitual/MAT_MoonCircle_Stone.mat`, `MAT_MoonCircle_Glow.mat`; `Relics/M_Relic_KatawGift_{Coral,SeaGlass,Shell}.mat`, `M_Relic_Pearl.mat`; `Visayas/M_GlowStone_{Cyan,Mint}.mat` |
| **Models** | `Models/Bakunawa_v2/` (`Bakunawa_v2.fbx`, `Materials/MAT_Bakunawa_v2.mat`, `Textures/Bakunawa_BaseColor.png`, `Bakunawa_Normal.png`); `Models/Castle/castle.fbx`; `Models/Dugong/Dugong_Albedo.jpg`; `Models/MC/MainPlayer_Albedo.jpg`; `Models/relics/Optimized/` (`KatawGift_Necklace_Mesh.asset`, `PearlOfLuminescence_Mesh.asset`, `PearlOfLuminescence_MetallicSmoothness.png`); `Models/relics/Pearl/` (`PearlOfLuminescence.gltf` + `.bin`, `Pearl_BaseColor.jpg`, `Pearl_MetallicRoughness.jpg`, `Pearl_Normal.jpg`) |
| **Prefabs** | `Prefabs/Creatures/PF_Bakunawa_v2_Model.prefab`; `Prefabs/UI/LalimTitle.prefab` |
| **Sprites** | `Sprites/Characters/bakunawa.png`, `kataw-female.png`, `kataw-male.png`, `old-kataw-female.png`, `old-kataw-male.png`; `Sprites/UI/Game Mechanics/icon_relic_pearl.png` |
| **UnderwaterMenu** | `SeabedMesh.asset`, `UW_PostProfile.asset`, `MenuPlayerAnimator.controller`, `Caustics.png`, `T_Bubble/T_Fish/T_Shaft.png`, `Castle_Albedo.jpg`, `M_Bubble`, `M_BubbleText`, `M_BubbleText_Splash`, `M_Castle`, `M_Fish`, `M_Shaft`, `UW_*.mat` (root) and `Materials/UW_*.mat` (16 variants) |
| **Docs (repo root)** | `CHANGELOG.md`, `CONTRIBUTING.md`, `README.md`, `LICENSE`, `DETAILED_UPDATES_2026-10-22.md` |
| **Other** | `Assets/Screenshots/` (29 PNGs), `Captures/`, `.obsidian/`, `graphify-out/` (probably not for the repo, see §2) |

### 8.3 Modified (selected notes)

| File(s) | Change |
|---|---|
| `Scripts/Challenges/Relocation/Bakunawa.cs` | +876/−26: full state machine, `ITalkPrelude`, own audio clips |
| `Scripts/Challenges/Relocation/BakunawaBody.cs` | +360/−15: procedural body, fin wiggle, trail |
| `Scripts/Challenges/Relocation/Sanctuary.cs` | +261/−23: capacity, glow, Bakunawa darkening |
| `Scripts/Challenges/Relocation/RelocationHud.cs` | +16/−69: release card removed; Compass bar shows name + distance, fades within ~60 m |
| `Scripts/Mechanics/Quest/Steps/SchoolRelocationStep.cs` | +307/−102: full minigame |
| `Scripts/Mechanics/Quest/Steps/SelectiveCatchingStep.cs` | +199/−49: 3 lessons + full test |
| `Scripts/Mechanics/Quest/Steps/FishGatheringStep.cs` | +36/−7: wiring |
| `Scripts/Mechanics/Quest/Steps/LevelExitStep.cs` | +24/−5: L2 exit flow |
| `Scripts/Mechanics/Quest/Steps/RelicRewardStep.cs` | +20/−5: `MoontideGiftSwirl` hook |
| `Scripts/Challenges/Catching/NetBag.cs` | +102/−5: capacity, wrong-catch penalty UI |
| `Scripts/Challenges/Catching/CatchableSchool.cs` | +25/−6: protected-species ID |
| `Scripts/Challenges/Gathering/PredatorAlertUI.cs` | +157/−19: threat bar, state machine |
| `Scripts/Challenges/Gathering/HidingZone.cs` | +25/−2: detection improved |
| `Scripts/Challenges/Gathering/ConchCall.cs` | +4/−0: await animation |
| `Scripts/Mechanics/StatueDialogueRouter.cs` | +124/−0: `ITalkPrelude`, camera, portrait lookup |
| `Scripts/Mechanics/TideCurrent.cs` | +128/−6: `StreakMaterial`, `All` collection |
| `Scripts/Mechanics/KatawVillager.cs` | +210/−1: look-at, rim fade, dialogue callbacks |
| `Scripts/Mechanics/ManunuboWisp.cs` | +75/−1: brightness coupling |
| `Scripts/Mechanics/SpiritSense.cs` | +39/−3: moon ritual target override |
| `Scripts/Mechanics/DiverIdleFloat.cs` | +27/−4: `IsIdle`, `IdleTime` properties |
| `Scripts/Mechanics/CameraController.cs` | +11/−5: Bakunawa focus override |
| `Scripts/Mechanics/CruisingAnimal.cs` | +13/−3: movement pause near camera |
| `Scripts/Mechanics/PollutionDirector.cs` | +17/−0: L2 pollution linkage |
| `Scripts/Mechanics/MarineSchool.cs`, `PlayerController.cs` | +1 line each (`LockMovement` hook in `PlayerController`) |
| `Scripts/Mechanics/Quest/Level2DialogueScripts.cs` | +97/−19: Ajuy + Padre Burgos drafts |
| `Scripts/Mechanics/Quest/LevelFlowDirector.cs` | +13/−0: L2 zone ordering |
| `Scripts/Mechanics/Quest/RelicPresentation.cs` | +21/−6: swirl timing |
| `Scripts/Abilities/DugongCompanion.cs` | +91/−20: idle float sync, `HoldAt` yaw |
| `Scripts/Abilities/AbilityBase.cs` | +27/−0: `LockMovement` API |
| `Scripts/Abilities/AbilityController.cs` | +5/−2 |
| `Scripts/Abilities/CheckpointManager.cs` | +23/−0: L2 checkpoint IDs |
| `Scripts/Abilities/SpiritWisp.cs` | +33/−6: ritual target override |
| `Scripts/Abilities/TidebondAbility.cs` | +23/−0 |
| `Scripts/Core/GameConstants.cs` | +4/−4: Visayas zone constants renamed (`VISAYAS_CALUYA/AJUY/PADRE_BURGOS`) |
| `Scripts/Core/GameEvents.cs` | +4/−1: `SceneLoadProgressEvent.Phase`; `ChallengeType.MoonRitual` |
| `Scripts/Core/LoadingStages.cs` | +12/−0: `Label(stage)` player-facing stage text |
| `Scripts/Core/SceneLoader.cs` | +8/−6: publishes `Phase` with each progress event |
| `Scripts/Core/ProximityMusic.cs` | +29/−1: Bakunawa tension music |
| `Scripts/DDA/Level2DifficultyKnobs.cs` | +6/−0: moon ritual knobs |
| `Scripts/Editor/RelicAssetBuilder.cs` | +136/−0: **Build Pearl of Luminescence** |
| `Scripts/Editor/StatueTalkFraming.cs` | +86/−0: Bakunawa camera focus override |
| `Scripts/UI/CalloutUI.cs` | +48/−3: speaker portrait support |
| `Scripts/UI/DialogueManager.cs` | +27/−3: Bakunawa portrait lookup |
| `Scripts/UI/GlassSprites.cs` | +36/−3: moon glyph sprite adapter |
| `Scripts/UI/SplashScreenUI.cs` | +6/−3: `LalimTitleAnimator` hook |
| `Scripts/UI/UiTapSound.cs` | +0/−4: `NoTapSound` moved to its own file |
| `Animations/DugongAnimator.controller` | `SwimForward` / `SwimReverse` replaced by one `Swim` state |
| `Animations/PlayerAnimator.controller` | `FloatSwim` state added |
| `Data/Difficulty/SelectiveCatching.asset` | New `schoolsToNet` knob (8 / 14 / 25) |
| `Data/Items/12_PearlOfLuminescence.asset` | `icon` linked to `icon_relic_pearl.png` |
| `Fonts/PlusJakartaSans-ExtraBold SDF Padded.asset`, `PlusJakartaSans-Medium SDF Padded.asset`, `Spicy Sale SDF.asset` | Font atlas updates |
| `Models/Bakunawa/Bakunawa.fbx`, `Models/Dugong/dugong.fbx`, `Models/MC/Player_final_final.fbx` (+ `.meta`) | Re-exported (dugong 61.8 → 0.6 MB, player 61.9 → 2.2 MB) |
| `Models/Bakunawa/Materials/MAT_Bakunawa_Body.mat` | Double-sided (cull off) |
| `Assets/Davis3D/LowpolyOceanPack/Materials/M_WaterSurface.mat` | Transparent, tinted blue (alpha 0.75) |
| `Models/sea creatures + elements (fbx)/SeaWeed.fbx.meta`, `WhaleShark/WhaleShark.fbx.meta` | Import settings |
| `Prefabs/Characters/Kataw/PF_Kataw_*.prefab` ×6 | Portrait sprites linked |
| `Prefabs/Characters/PF_Dugong.prefab` | `M_Dugong.mat` applied |
| `Prefabs/LevelFlow/PF_DialogueSystem.prefab` | L2 portraits linked |
| `Prefabs/LevelFlow/PF_FoggyBarrier.prefab` | Material refresh |
| `Prefabs/PF_GameCamera.prefab` | Idle-float camera tie-in |
| `Prefabs/Player_final_final.prefab` | `M_MainPlayer.mat` applied |
| `Prefabs/Relics/PF_Relic_KatawGift_PH.prefab`, `PF_Relic_PearlOfLuminescence.prefab` | Real mesh + materials |
| `Prefabs/UI/GameHUD.prefab` | Minor layout refresh |
| `Resources/Audio/AudioLevels.asset` | Updated volume preset |
| `Scenes/01_SplashScene/01_Splash.unity` | `LalimTitleAnimator` wired |
| `Scenes/02_MainMenu/02_MainMenu.unity` | Rebuilt as underwater menu scene |
| `Scenes/Levels/01_Luzon/02_Luzon.unity` | Minor quest-step / dialogue updates |
| `Scenes/Levels/02_Visayas/03_Visayas.unity` | Bakunawa_v2, village, glow stones, currents, level pieces, ritual + Bakunawa audio |
| `Scenes/Tutorial/01_Tutorial.unity` | Camera and dialogue minor updates |

### 8.4 Deleted

| File | Why |
|---|---|
| `1_sample.md` (repo root) | Scratch file; flagged as "probably not for the repo" in the Oct 8 doc. |
| `nextsteps (aug 4).md` (repo root) | Old to-do notes (158 lines), superseded by these docs. |

---

## 9. Rules & conventions

All rules from the Oct 4 (1–12) and Oct 8 (13–20) documents apply ([Oct 4 §9](DETAILED_UPDATES_2026-10-04.md#9-rules--conventions), [Oct 8 §9](DETAILED_UPDATES_2026-10-08.md#9-rules--conventions)). Additions:

21. **`PlayFromZone` is mandatory for Level 2 playtests.** Never manually edit the save for a Level 2 test run.
22. **`MoonPhaseRitualStep` phase shuffle is session-specific.** Don't hard-code circle positions or expected phase layout.
23. **`WaterBrightness.Contrast` not `RenderSettings.fogColor` directly.** Any VFX or particle that needs to adapt to water brightness must read `WaterBrightness.Contrast`.
24. **Use `NoTapSound` to silence gameplay buttons.** Put it on the panel's parent (it now lives in `Scripts/UI/NoTapSound.cs`). Don't add `isGameplayButton` or similar per-button flags to MonoBehaviours.
25. **`ITalkPrelude` for all dramatic NPC entrances.** Any NPC with a pre-conversation animation implements `ITalkPrelude`. Don't bespoke-wire `StatueDialogueRouter`.
26. **Bakunawa v1 model kept for reference.** Do not delete `PF_Bakunawa_Model.prefab` or `Bakunawa.fbx`. Use `PF_Bakunawa_v2_Model.prefab` for new scene work.
27. **Level 2 builders rebuild from scratch.** `Level2KatawVillage`, `Level2Currents`, `Level2LevelPieces` and `Zone3GlowStones` remove their own earlier output before rebuilding. Manual edits to their output will be lost on the next build.
28. **`graphify-out/` (both copies), `Assets/Screenshots/`, `.obsidian/`, `Captures/` must not be committed.** Add them to `.gitignore`.
29. **Bakunawa audio lives on `Bakunawa.cs`.** Assign its clips on the component (or through its `EditorSet…` helpers), not in `SfxLibrary`.

---

## 10. Known issues & open questions

- **`03_Visayas.unity` has not been play-tested end to end.** Zone 3 quest steps are still being wired.
- **No Padre Burgos zone BGM.** `Audio/Music/PadreBurgos/` holds only the ritual melodies and lullaby.
- **Kataw Gift ability** — model and icon are done; equipped-relic action does nothing. TBD by design.
- **Pearl of Luminescence ability** — relic model is done; the gameplay action (light-based ability) is not designed or implemented.
- **`Level2DialogueScripts` is a draft** — needs a writer's pass. `{player}` token not resolved in all Padre Burgos lines.
- **`graphify-out/` and `Captures/`** not yet in `.gitignore` — block push until resolved.
- **`.obsidian/`** at root — team must decide: track (shared vault config) or ignore.
- **Android shader warm-up** — no `.graphicsstate` recordings for Android Vulkan/GLES yet.
- **Two castle models**: `Models/Castle/castle.fbx` (menu) and `Models/Environment/Castle/Castle.fbx` (Level 1) are different files. Decide whether both are needed.
- **Level 2 builders without menu items** (`Level2Currents`, `Level2KatawVillage`, `Level2LevelPieces`) — add menu items or keep them code-only.
- Earlier open issues not resolved above still apply. See [DETAILED_UPDATES_2026-10-08.md §10](DETAILED_UPDATES_2026-10-08.md#10-known-issues--open-questions) and [DETAILED_UPDATES_2026-10-04.md §10](DETAILED_UPDATES_2026-10-04.md#10-known-issues--open-questions).

---

## 11. Footnotes

[^tests]: The `Lalim.Tests` filter matters: unfiltered runs include URP package tests, and `EditorTests.ValidateDefaultRendererAfterReset` hangs.

[^playfromzone]: `PlayFromZone` copies the real slot-0 save to `lalim_save_slot_0_before_play_from_zone.json` (and SaveManager's own `_backup` copy to `lalim_save_slot_0_backup_before_play_from_zone.json`) next to the real save, then writes the synthetic save. If **Restore My Save** is greyed out after a crash, copy the `_before_play_from_zone` file back manually.

[^moonritual]: Moon phase order (always 0→7): New Moon → Waxing Crescent → First Quarter → Waxing Gibbous → Full Moon → Waning Gibbous → Last Quarter → Waning Crescent. The circles are shuffled; the stepping order is always 0→7.

---

*Generated 2026-10-22 from the working tree (`git status` against `e35e93a`) and the session history. If something here disagrees with the code, the code wins — please update this file.*
