# DETAILED UPDATES — 2026-10-08

**Branch:** `hym-test`
**Last commit (baseline):** `c16e4fe` "complete level 1" (2026-10-04)
**Covers:** every uncommitted change in the working tree from 2026-10-04 to 2026-10-08, in the order the work happened.
**Size (not counting `.meta` files):** ~86 files/folders added · 98 modified · 3 deleted.

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

- **Audio system — full session-wide music and SFX:**
  - `MusicDirector`: a `DontDestroyOnLoad` singleton carrying BGM seamlessly across every scene load, with crossfades between tracks. A `MusicLibrary` asset (`Resources/Audio/MusicLibrary`) says what each scene sounds like (Track, KeepPrevious, Loading, OwnMusic, or Level mode).
  - `ZoneAmbience`: per-zone stereo beds crossfading when the diver moves between zones.
  - `Sfx` + `SfxLibrary` (`Resources/Audio/SfxLibrary`): a single shared cue set (Correct, Wrong, ItemPickup, MinigameStart, MinigameComplete, AltarRise, Reward, Hint, ButtonTap, ActTitle, ZoneCleansed, Teleport…). Reacts automatically to `ChallengeCompletedEvent` / `ChallengeFailedEvent`.
  - `UiTapSound`: session-wide singleton that plays `ButtonTap` on any menu button or dialogue tap without each screen wiring it.
  - `AmbientOneShots`: fire-and-forget positional sounds for one-off scene events.
  - `AudioLevels`: the four mixer volume knobs (Music, SFX, Ambient, Voice), keyed by the settings system.
  - Audio assets in `Assets/Lalim/Audio/`: `Music/MUS_Intro.mp3`; `Ambient/` and `SFX/` each split into `Luzon/`, `Visayas/` and `Shared/` subfolders.
- **Surge / Riptide redesign — swim stamina bar:**
  - `SwimStaminaPool` (new, pure-math, unit-testable): a 6-segment bar that surges **charge** and Riptide **spends**. A 1-surge combo adds ⅓ of a segment; 2-surge adds ½; 3-surge adds 1 whole segment. Riptide burns whole segments at 10 s each.
  - `SurgeAbility` now owns a `SwimStaminaPool`; the 7 s cooldown only fires after the 3rd surge of a combo. Tidebond cooldown tuned down from 30 s to **7 s**.
  - `PauseClock` (new): real-time clock that stops while the pause menu is open; HUD animations (toasts, banners, cards) use this instead of Unity's unscaled time so a game-pause freezes them too.
- **Inventory, relics in the HUD, and equipped-relic actions:**
  - `InventoryModalUI` (rewritten from the old deleted stub): a runtime-built frosted-glass window listing Relics and Tools as cards; tap a card to see it on the right with an Equip/Unequip button for relics. World pauses while open.
  - `RelicLoadout` (new): static tracker for the one equipped relic, its HUD button, and per-relic cooldowns. Saved in `SaveData.equippedRelicIds`.
  - `RelicHudButtons` (new): the equipped relic's HUD button, matching the skill-bubble style; shows a radial cooldown sweep. Key **1** presses it on desktop. Hold events published as `RelicHoldEvent` for net-throw aim.
  - `RelicIcons` (new): procedurally drawn icons (spiral shell, knotted mesh, compass ring+needle) for the three Level 1 relics — no art needed.
  - `ItemPortraits` (new): renders a relic's world-model prefab to a 256×256 `RenderTexture` (three-quarter view) for the inventory card portrait; falls back to the item's icon sprite.
  - `GameConstants.Relics` expanded with Level 2 relic IDs: `MOONTIDE`, `KATAW_GIFT`, `PEARL_OF_LUMINESCENCE`; placeholder item assets `10_Moontide`, `11_KatawGift`, `12_PearlOfLuminescence` added.
  - Relic icon sprites added: `icon_relic_compass.png`, `icon_relic_conch.png`, `icon_relic_net.png`, `relic_button_bg.png`.
- **Level 2 (Visayas) groundwork — three minigames, models, world, dialogue:**
  - World model split into zones: `Visayas_Zone1_Caluya.fbx`, `Visayas_Zone2_Ajuy.fbx`, `Visayas_Zone3_Padre_Burgos.fbx`, `Visayas_Zone4_Cave_Tunnel.fbx`, terrain and trenches.
  - Characters: **Bakunawa** (the sea-serpent boss; `Bakunawa.fbx`), **Kataw** merfolk (6 body types: male/female/child/old; rigs and animations in `Models/Kataw/`), their prefab folder and `Prefabs/Characters/Kataw/`.
  - **Minigame 1 — Fish Gathering (Caluya):** `FishGatheringStep`, `Herd`, `HerdRibbon`, `ConchCall`, `HidingZone`, `Predator`, `PredatorAlertUI`. The Radiant Conch calls scattered herds; predator sharks patrol the open sand; hiding zones in seagrass or narrow rocks give safety. Manunubo's spirit (`ManunuboWisp`) drifts around home and brightens as herds return.
  - **Minigame 2 — Selective Catching (Ajuy):** `SelectiveCatchingStep`, `CatchableSchool`, `NetBag`, `NetThrower`, `RootPool`. The Woven Net Fragment is thrown (hold to aim along a physics arc, release to cast); Spirit Sense identifies green targets and red protected animals. Three Kataw lessons (throw, identify, patience) lead to the full test.
  - **Minigame 3 — School Relocation (Padre Burgos):** `SchoolRelocationStep`, `Bakunawa` (boss), `BakunawaBody`, `Sanctuary`, `RelocationHud`. Bakunawa's awakening drops every barrier and scatters rocks. Caught schools ride in the net-bag; each extra school slows the diver and extends Bakunawa's sense range. The Compass points to the nearest correct sanctuary (passive, stops within 60 m). Appeasement: all schools home, then the diver plays the Radiant Conch for Bakunawa in its lair.
  - **`Level2DialogueScripts`**: placeholder lore for Manunubo, the Kataw Elder (+ teachers Amihan, Lakan, Inang Sela, Dalisay), and Bakunawa; formatted with `{player}` token replacement.
  - **DDA profiles** for the new minigames: `FishGathering.asset`, `SchoolRelocation.asset`, `SelectiveCatching.asset`. `Level2DifficultyKnobs` defines their knob names.
  - **`03_Visayas.unity`** scene created (not yet built out; placeholder only).
- **Level template builder — reuse for Levels 2 & 3:**
  - `LevelTemplateBuilder` (**Tools ▸ Lalim ▸ Level Template ▸**): Build Template Prefabs (saves per-manager `Core/` prefabs and `PF_ZoneTemplate`/`PF_ZoneTemplate_Final`); Create Template Scene (`Scenes/Levels/_Template/00_LevelTemplate.unity`); Add Missing Level Pieces To Open Scene; Fix Up Level IDs (stamps zone/step/checkpoint IDs from the region — required before any play-test of a duplicated template).
- **Creature animation pipeline:**
  - `CreatureRig`: plays a rigged creature's swim/idle clip via `PlayableGraph`, starting at a random point so schools don't animate in lockstep.
  - `SeaCreatureClips` (`Resources/SeaCreatureClips.asset`): maps model name → swim + idle clip for runtime-spawned animals.
  - `RiggedSeaCreatures` editor tool (**Tools ▸ Lalim ▸ Use Rigged Sea Creatures**): builds `SeaCreatureClips`, swaps Luzon's static baked-mesh creatures for their rigged FBX counterparts, updates wanderer prefabs.
  - `AmbientSwimmers`: drives Visayas's baked-in sea life by name fragment in slow loops; only creatures near the camera move.
  - `CruisingAnimal`: wide open-water loops for large animals (whale sharks). New FBX: `Shark.fbx`, `WhaleShark/`.
- **Diver & NPC feel — idle float, IK, Kataw villagers:**
  - `DiverIdleFloat`: slow bob + sway + upright lean (72°) when the diver stops; only the model moves, never the collider.
  - `TwoBoneIK`: analytic two-bone IK for Generic rigs without a Humanoid avatar; run in `LateUpdate` after the Animator.
  - `KatawVillager`: idle-bob, watch-the-diver turn, optional amber rim light for quest givers, optional proximity hum.
  - `ManunuboWisp`: drifting wisp on looping curves; hovers and stills when the player draws near; brightness driven by the gathering minigame.
- **Tide currents (Moontide gift):** `TideCurrent` — a path-based tube of moving water. Pre-Moontide: invisible and inert. Post-Moontide: glows as moonlit streaks and carries the diver at 9 m/s (eases in/out). A gentle-push mode (0.5–1 m/s, always on) for the Trench A eastward nudge.
- **Minigame UX:** `CalloutUI` (non-blocking frosted-pill callout during minigames, queued), `HintBulb` (now a proper shared component, card-attach and standalone modes), `SortingStation` (Visayas sorting variant placeholder).
- **Scene/rendering:** `TransitionScreen` (live-canvas crossfade, no screenshot capture), `FollowCameraInBounds` (clamps a particle emitter to the camera inside a zone volume).
- **Scene tools:** `SolidSceneryColliders` (mesh colliders on solid scenery by name), `Level1ZoneSites` (geometry sampler for zone 2 `ZoneSiteShuffle` pool), `ZoneSiteShuffle` (per-playthrough minigame area randomisation with saved draw).
- **Logos/metadata:** `koral_labs.png` deleted (old logo), `black_cat.png` added. `CLAUDE.md` and `1_sample.md` added to repo root.

---

## 2. ⚠️ Read before you pull / commit

| # | Issue | What to do |
|---|---|---|
| 1 | **86 new files are untracked** (all the Level 2 work, audio system, new scripts, models). None are staged. | Review and `git add` deliberately; don't `git add .` without checking — `Assets/Screenshots/`, `Assets/_AxisTest/`, `.claude/`, `1_sample.md` and `Assets/Lalim/Scripts/Core/graphify-out/` are probably not for the repo. |
| 2 | **`03_Visayas.unity`** exists but the scene is not built out yet. | It's a placeholder. Don't expect it to play through. |
| 3 | **Level 2 dialogue is PLACEHOLDER** (`Level2DialogueScripts.cs` says so in its header). | Real script from the writer is needed before the level is content-complete. |
| 4 | **`MusicLibrary` and `SfxLibrary` ScriptableObjects are empty** — they exist at `Resources/Audio/` but have no clips assigned. | Assign clips before any level's audio will work. |
| 5 | **`SwimStaminaPool` changes how Surge and Riptide interact.** Riptide now requires at least one full segment (one 3-surge combo) before it runs. | To give the player a full bar for testing, call `SurgeAbility.Swim.Refill()` from a debug tool. |
| 6 | **Tidebond cooldown changed from 30 s → 7 s** in `GameConstants`. | Any design doc or test that expected 30 s needs updating. |
| 7 | **`CodexModalUI.cs` and `MapModalUI.cs` deleted** from the working tree. | Any scene or prefab that referenced them will have missing-script errors. |
| 8 | **`Prefabs/Characters/Kataw/`** and **`Models/Kataw/`** are new, untracked. | Add the whole folder together (they share materials and textures). |
| 9 | **`SeaCreatureClips.asset`** (at `Assets/Lalim/Resources/SeaCreatureClips.asset`) is untracked. | Run **Tools ▸ Lalim ▸ Use Rigged Sea Creatures** in 02_Luzon to (re)build it, then add to git. |
| 10 | All other baseline warnings from the Oct 4 doc (§2 items 1–9) still apply: local MCP package path, crash-recovery scenes, stray assets, embedded URP edit, Git LFS for large binaries, save-file location. | See [DETAILED_UPDATES_2026-10-04.md](file:///Users/admin/Documents/LALIM/DETAILED_UPDATES_2026-10-04.md) §2. |

---

## 3. Quick start

**Play the game (Level 1 only, fully working):** open `Scenes/00_BootSequence/00_BootSequence.unity` and press Play.

**Play Level 1 directly:** open `Scenes/Levels/01_Luzon/02_Luzon.unity` and press Play.
- Tidebond cooldown is now 7 s; Surge charges the swim bar (do a 3-surge combo to give Riptide fuel).

**Explore Level 2 (world models only):** open `Scenes/Levels/02_Visayas/03_Visayas.unity`.
- Scene is a placeholder — no gameplay systems are placed. FBX assets are in `Models/Environment/Visayas/`.

**Run tests:** Window ▸ General ▸ Test Runner ▸ EditMode, filtered to **`Lalim.Tests`**.[^tests]

**Open the inventory in-game:** press **I** (desktop) or tap the inventory button on the HUD. Equip a relic from the Relics tab to get its HUD button.

**Dev hotkeys** (editor and development builds):

| Key | Action |
|---|---|
| F9 | Ability/vitals readout |
| F10 | Infinite stamina |
| F11 | Cycle difficulty 0 / 0.5 / 1 |
| I | Toggle inventory modal |
| 1 | Press the equipped relic's HUD button |

---

## 4. Timeline — earliest to latest

> Dates are approximate. Paths are under `Assets/Lalim/` unless otherwise shown.

### 4.1 Swim stamina bar, Tidebond tuning, PauseClock (2026-10-05)

**`SwimStaminaPool`** (new, `Scripts/Abilities/`): pure-math 6-segment pool shared between `SurgeAbility` and `RiptideAbility`. Rules:
- **Surge charges it**: 1st surge of a combo = +⅓ seg; 2nd = +⅙; 3rd = +½ (totals: ⅓, ½, 1 full seg).
- **Riptide spends it**: each whole segment = 10 s of speed boost. Fractional segments are preserved.
- **Refills on respawn** (`PlayerRespawnedEvent`). Saved as a 0–1 fill alongside other vitals.
- The 7 s cooldown on `SurgeAbility` now only fires after the 3rd surge of a combo; a 1- or 2-surge combo simply resets its window when it lapses (no cooldown).

**`SurgeAbility` updated**: owns the `SwimStaminaPool` as `Swim`; exposes `RiptideSecondsPerSegment` from config. `RiptideAbility` reads `Surge.Swim` to know how long to run.

**`GameConstants.Abilities`** gains `SWIM_STAMINA_SEGMENTS = 6` and `RIPTIDE_SECONDS_PER_SEGMENT = 10`; `TIDEBOND_COOLDOWN` drops from 30 s → **7 s**.

**`PauseClock`** (new, `Scripts/Core/`): static clock counting unscaled real time that stops while `timeScale == 0` (the pause menu). Gives `DeltaTime` (0 while paused) and `Time` (monotonic, never advances while paused). HUD widgets that previously used `Time.unscaledDeltaTime` use this instead, so the pause menu freezes them too.

### 4.2 Audio system (2026-10-05 → 10-06)

The project previously had no session-wide audio management. Five components and two ScriptableObject types were added.

**`MusicDirector`** (`Scripts/Core/`): `DontDestroyOnLoad` singleton (boots via `RuntimeInitializeOnLoadMethod`) with two `AudioSource` channels (`_a` current, `_b` fading out). Crossfade: 2.5 s. Logic:
- **New Game**: loading screen starts the intro BGM; it carries through Name Input, Tutorial and the first Teleport without restarting.
- **Continue/Load**: loading screen starts the saved level's BGM.
- **Loading screens**: whatever was playing carries on seamlessly.
- **Levels**: `ProximityMusic` takes over at the exact sample position and hands back when the level unloads (level → loading is seamless).
- **Main Menu**: plays its own music; the carried track fades out.

**`MusicLibrary`** (`Resources/Audio/MusicLibrary.asset`): `ScriptableObject` with a `List<SceneMusic>` mapping scene names to `Mode` (Track, KeepPrevious, Loading, OwnMusic, Level) and optional `bgm`/`ambience` clips.

**`ZoneAmbience`** (`Scripts/Core/`): created lazily on the first `ZoneAmbience.Play(clip)` call; lives in the level scene. Crossfades between zone ambient beds (3 s) as `ZoneQuest` changes.

**`Sfx`** (`Scripts/Core/`): static class. Listens to `ChallengeCompletedEvent`, `ChallengeFailedEvent`, `HintRequestedEvent` on `AfterSceneLoad`. `Sfx.Play(SfxCue)` or `Sfx.PlayAt(SfxCue, Vector3)` plays the clip from `SfxLibrary` through a pooled `AudioSource`.

**`SfxLibrary`** (`Resources/Audio/SfxLibrary.asset`): maps each `SfxCue` enum value to an `AudioClip` and a volume. Cue list: `Correct`, `Wrong`, `ItemPickup`, `MinigameStart`, `MinigameComplete`, `MinigameFailed`, `AltarRise`, `Reward`, `Hint`, `ButtonTap`, `ActTitle`, `ZoneCleansed`, `Teleport`.

**`AmbientOneShots`** (`Scripts/Core/`): `AmbientOneShots.PlayAt(clip, pos, volume, range)` places a temporary `AudioSource`, plays once, and destroys itself. For one-shot scene events.

**`AudioLevels`** (`Scripts/Core/`): exposes the four mixer float parameters (Music, SFX, Ambient, Voice) as typed properties with `[0, 1] → dB` conversion.

**`UiTapSound`** (`Scripts/UI/`): `DontDestroyOnLoad` singleton (boots via `RuntimeInitializeOnLoadMethod`). Intercepts pointer-down/up pairs; plays `SfxCue.ButtonTap` on any `Button` or `Toggle` not under a `NoTapSound` component. `DialogueManager` also calls it when a tap advances dialogue.

**Audio folder layout:**

| Path | Contents |
|---|---|
| `Audio/Music/` | `MUS_Intro.mp3` (opening BGM, New Game through first Teleport) |
| `Audio/Ambient/Luzon/` | Luzon per-zone ambient beds (to assign in `MusicLibrary`) |
| `Audio/Ambient/Visayas/` | Visayas per-zone beds |
| `Audio/Ambient/Shared/` | Clips used in more than one level |
| `Audio/SFX/Luzon/` | Luzon-specific SFX |
| `Audio/SFX/Visayas/` | Visayas-specific SFX |
| `Audio/SFX/Shared/` | Cue clips wired into `SfxLibrary` |

### 4.3 Inventory modal, relic loadout, relic HUD (2026-10-06)

**`InventoryModalUI`** (rewritten; `Scripts/UI/`): the old `CodexModalUI` and `MapModalUI` are deleted. The new inventory is a runtime-built frosted-glass window (same visual language as minigame cards):
- Two sections (Relics, Tools) as card rows in a grid.
- **Card**: item name + kind badge. Portrait is a `RenderTexture` of the item's world model (`ItemPortraits`) or its icon sprite.
- **Detail panel** (right): portrait, name, kind, description, optional Equip/Unequip button for equippable relics.
- **Equip**: calls `RelicLoadout.Equip(itemId)`; equipping a second relic swaps the first out.
- **Pauses the world** while open (`timeScale = 0`). Opened by HUD inventory button or **I** key; toggled.
- No art required: built entirely in code using `GlassSprites`.

**`RelicLoadout`** (`Scripts/Inventory/`): static class. `Equip(itemId)` / `Unequip(itemId)` / `IsEquipped(itemId)`. Publishes `RelicEquipChangedEvent`. Persists as `SaveData.equippedRelicIds`. Exposes `IsEquippable` (Radiant Conch, Woven Net Fragment, Compass) and `HasButton` (Conch, Net — the Compass is passive). Also manages per-relic cooldowns (`StartCooldown`, `Normalized` 0→1, `IsReady`).

**`RelicHudButtons`** (`Scripts/UI/`): one button, shown only while a relic with an action is equipped. Positioned at `(−592, 258)` relative to the canvas bottom-right corner. Tap → `RelicLoadout.Use()` → `RelicUsedEvent`. Hold/release → `RelicHoldEvent` (net thrower aims on hold). Follows Right-Handed Mode. Key **1** on desktop.

**`RelicIcons`** (`Scripts/UI/`): procedural icons drawn to a 256×256 `Texture2D` then sliced to a `Sprite`. Three shapes: spiral-shell Conch, knotted-mesh Net Fragment, ring+needle Compass. All drawn as white shapes, anti-aliased, tinted cream at runtime.

**`ItemPortraits`** (`Scripts/UI/`): lazy-rendered 256×256 `RenderTexture` of the item's `worldModelPrefab`. Temporary off-screen camera, three-quarter view, clear background. Cached by item ID; silhouette variant (white shape) available for icon fallback.

**`GameConstants.Relics`** gains:
- `MOONTIDE = "10_moontide"` — Manunubo's gift; unlocks tide current visibility and carry.
- `KATAW_GIFT = "11_katawgift"` — Kataw Elder's gift; placeholder, design TBD.
- `PEARL_OF_LUMINESCENCE = "12_pearlofluminescence"` — Bakunawa's gift.

Item assets: `Data/Items/10_Moontide.asset`, `11_KatawGift.asset`, `12_PearlOfLuminescence.asset`.
Placeholder prefabs: `Prefabs/Relics/PF_Relic_Moontide_PH.prefab`, `PF_Relic_KatawGift_PH.prefab`, `PF_Relic_PearlOfLuminescence.prefab`.
Sprites: `Sprites/UI/Game Mechanics/icon_relic_compass.png`, `icon_relic_conch.png`, `icon_relic_net.png`, `relic_button_bg.png`.

### 4.4 Level 2 Visayas — world models, characters, deities (2026-10-06 → 10-07)

**World model** (`Models/Environment/Visayas/`): eight FBX files — terrain base, three connective trenches (TrenchA, TrenchB), three zones, and a cave tunnel. Mangroves in a sub-folder. Visayas materials in `Materials/Visayas/`.

**Bakunawa** (`Models/Bakunawa/Bakunawa.fbx`): the moon-eating sea-serpent, Level 2's boss. Prefab: `Prefabs/Creatures/PF_Bakunawa_Model.prefab`.

**Kataw merfolk** (`Models/Kataw/`): six rigged character bodies (Male, Female, ChildMale, ChildFemale, OldMale, OldFemale). Animations in `Models/Kataw/Animation/`. Prefab folder: `Prefabs/Characters/Kataw/`.

**`Manunubo.png`** (sprite in `Sprites/Characters/`): Caluya spirit's dialogue portrait.

**`Prefabs/Creatures/PF_Wanderer_WhaleShark.prefab`**: a whale shark on a wide `CruisingAnimal` loop.

### 4.5 Level 2 minigames (2026-10-07)

All three implement `IAreaMinigame` / extend `QuestStep` and use the shared `AreaStation` / `MinigameStage` pattern from Level 1.

#### Minigame 1 — Fish Gathering (Caluya zone; `Challenges/Gathering/`)

`FishGatheringStep` (a `QuestStep`): the player blows the Radiant Conch (equipped relic) to calm scattered herds and guide them toward Manunubo's light.

| Class | Role |
|---|---|
| `Herd` | One panicked school: fish models in a frightened cluster; calmed by `ConchCall`, guided by the ribbon. |
| `HerdRibbon` | A bioluminescent trail from the player to Manunubo when a herd is calmed and following; deliver fires when the ribbon reaches Manunubo. |
| `ConchCall` | The Radiant Conch's action: pulses a calm wave settling herds within range. Cooldown via `RelicLoadout`. |
| `HidingZone` | A safe area (seagrass, tight rocks): predators inside de-aggro. |
| `Predator` | A patrolling shark. Detects and strikes the player in open water; evaded in a hiding zone or by mounting the dugong (predators treat a mounted diver as part of the herd). |
| `PredatorAlertUI` | Proximity-warning chevron pointing toward the nearest aggro'd predator. |

`ManunuboWisp` (`Scripts/Mechanics/`): Manunubo lives on this component. Drifts on looping curves; stills/hovers when the player comes within `_calmRadius`. `SetBrightness(float)` called by `FishGatheringStep` as herds return — dim at start, full glow when all are home.

DDA: `FishGathering.asset` — knobs for herd count, predator speed and sense range, hiding zone effectiveness, conch range and cooldown.

#### Minigame 2 — Selective Catching (Ajuy zone; `Challenges/Catching/`)

`SelectiveCatchingStep` (a `QuestStep`): three Kataw lessons then a four-pool test.

| Class | Role |
|---|---|
| `RootPool` | One pool: a cylinder of water with a set of fish and optional protected animals. |
| `CatchableSchool` | A school or protected animal in a pool; Spirit Sense tints green (catch) or red (protected). |
| `NetThrower` | Physics-arc throw (hold relic button to aim, release to throw); net opens at the landing point and closes around everything inside. |
| `NetBag` | Tow-bag of caught schools trailing behind the diver; each extra school adds 10% speed penalty and extends Bakunawa's sense range. |

Kataw teacher villagers (Amihan, Lakan, Inang Sela, Dalisay) use `KatawVillager` and `StatueDialogueRouter`. Quest Kataw have an amber rim light.

DDA: `SelectiveCatching.asset` — net size, throw cooldown, fish speed, protected animals per pool, school size; up to 3 errors before the test restarts.

#### Minigame 3 — School Relocation (Padre Burgos zone; `Challenges/Relocation/`)

`SchoolRelocationStep` (a `QuestStep`): the climax of Level 2.

| Class | Role |
|---|---|
| `Bakunawa` | The boss serpent: patrols, alerted by school count + proximity; a strike bursts the net-bag, scattering schools as panicked yellow glows. Manages phases (patrol, chase, strike, retreat) and calls the roar. |
| `BakunawaBody` | Body segments: a tail-following chain, animates and undulates as the head moves. |
| `Sanctuary` | A safe coral alcove or cave sized for the right schools; glows when accessible. Right school absorbed; wrong sanctuary triggers burst. |
| `RelocationHud` | Compass arrow to the nearest correct sanctuary (passive; stops within 60 m so the player must deduce the match). |

**Awakening sequence**: Bakunawa roars (after catching-step dialogue), barriers drop, rocks fall, diver must escape the abyss above y −110.

**Appeasement**: all schools home → Bakunawa retreats to its lair → player swims there and plays the Radiant Conch → sea settles.

DDA: `SchoolRelocation.asset` — Bakunawa's speed, sense range, strike damage, calm/rubber-band times; schools carried at once (metric).

### 4.6 Level 2 dialogue, tide currents (2026-10-07)

**`Level2DialogueScripts`** (`Scripts/Mechanics/Quest/`): static class with placeholder dialogue for all three Level 2 spirits. Formatted with `{player}` token and `isPlayer` flag, identical to the Level 1 pattern. Barks for report-back and relic-gift moments. ⚠️ **All lines are placeholder — the real script is not written yet.**

**`TideCurrent`** (`Scripts/Mechanics/`): a path-based tube of moving water.
- **Pre-Moontide**: invisible, no carry. `MoontideOwned` checks `InventoryManager.HasItem`.
- **Post-Moontide**: glows as soft moonlit streaks (particle system using `_streakMaterial`); entering the tube → carried at `_carrySpeed` (default 9 m/s), easing in at the start and out over the last metres.
- **Gentle mode** (`_gentleSpeed`): a small always-on push (e.g. Trench A east flow, 0.5 m/s) with drifting motes even before Moontide.
- Registered in a static `_all` list so `PlayerController` can check all active currents in one pass.

### 4.7 Creature animation pipeline, idle float, Kataw feel (2026-10-07 → 10-08)

**`CreatureRig`** (`Scripts/Mechanics/`): adds an `Animator` and plays a clip via `PlayableGraph` at a random start time. Static factory `CreatureRig.Play(instance, modelName, idle, speed)` looks up the clip in `SeaCreatureClips`.

**`SeaCreatureClips`** (`Scripts/Mechanics/`): `ScriptableObject` at `Resources/SeaCreatureClips`. Built by `RiggedSeaCreatures`. Supported species: BrownFish, ClownFish, DoryFish, TunaFish, Turtle (separate idle clip), StingRay, Hammerhead, Whale, and others. Ghost-net and school creatures call `SeaCreatureClips.Find(modelName, idle)`.

**`AmbientSwimmers`** (`Scripts/Mechanics/`): for scenes where creatures are baked into the environment model (Visayas). Finds them by name fragment (`fish_X_`, `ray_X_`, `turtle_X_`, `whaleshark`) and drives slow circular loops with bob, sway and tail wiggle. Only creatures within `_activeRange` of the camera move.

**`CruisingAnimal`** (`Scripts/Mechanics/`): wide elliptical loop for large open-water animals (whale sharks in Padre Burgos), independent of the seabed.

**`DiverIdleFloat`** (`Scripts/Mechanics/`): settles in 0.4 s of stillness; bobs 0.18 m at 0.8 Hz; sways 3°; tilts upright 72° (matching the Kataw idle pose). Only the visual body moves — never the collider. Resets instantly on any input.

**`TwoBoneIK`** (`Scripts/Mechanics/`): static analytic two-bone IK (upper arm → forearm → hand) for Generic rigs. Called in `LateUpdate` after the Animator; `weight` blends between the Animator's pose and the IK solution.

**`KatawVillager`** (`Scripts/Mechanics/`): placed on every Kataw NPC body.
- Bobs in a random-phase idle.
- Rotates to watch the diver within `_watchWithin` metres, back to default yaw when the diver leaves.
- Quest-giver flag: adds a soft amber point `Light` so quest Kataw read through silt.
- Optional proximity hum: a 3D `AudioSource` playing `_hum`, inaudible beyond `_humRange`.

**`RiggedSeaCreatures`** editor tool (**Tools ▸ Lalim ▸ Use Rigged Sea Creatures**):
1. Builds/updates `Resources/SeaCreatureClips.asset`.
2. Updates wanderer prefabs (`PF_Wanderer_Turtle`, `PF_Wanderer_StingRay`, `PF_Wanderer_WhaleShark`) to use `CreatureRig`.
3. In the open scene, finds `LuzonScene/Creature_*` static mesh groups, disables them, instantiates the matching rigged FBX at the same transform. Kinds without a rigged match and ghost-net animals are left as static. Safe to re-run.

### 4.8 Minigame UX refinements (2026-10-08)

**`CalloutUI`** (`Scripts/UI/`): a session-level singleton (lazy, built on first `Say()` call). A frosted-glass pill 12% from the top of the screen. Shows `"<Speaker>: <line>"` for `seconds` (default 3.6). Lines queue; a new line from the same speaker replaces any queued one (no pile-up). `Clear()` dismisses immediately. Does not interrupt play.

**`HintBulb`** (revised to shared component): two factory methods:
- `AttachToCard(card, height, onHint, font)` — flush-left at a card's edge behind a hairline divider. Widen the card by `HintBulb.CardZone` (118 px) and shift its content right.
- `CreateButton(parent, onHint, font)` — a standalone glass button.
Badge counts remaining hints; greyed out at 0. The minigame calls `ChallengeManager.UseHint()` in `onHint`, which plays the hint sound.

**`SortingStation`** (`Scripts/Mechanics/Quest/`): a Visayas variant of `SegregationAltar` for zone 2 of Level 2 (not yet placed; placeholder for the scene builder).

### 4.9 Scene tools, rendering helpers, template builder (2026-10-08)

**`SolidSceneryColliders`** (`Scripts/Editor/`): adds `MeshCollider` to solid geometry by name prefix. **Solid**: `Arch_`, `Wall_`, `Well_`, `Reef_flatrock`, `Reef_rockflat`, `Prop_Whale_Bone`, `Prop_WWII_Shipwreck`, `Prop_Kataw_Spire`, `Rock`, `Cliff`, `Boulder`, `Pillar`, `Column`. **Skip**: `Layout_`, `Baseline_`, `VFX_`, `Creature_`, `Minigame`, `Portal`, `Blocker`, `Fog`; objects already with a collider are left alone. Undo-able; re-running adds only what's missing.

**`Level1ZoneSites`** (`Scripts/Editor/`): geometry-aware sampler for zone 2's `ZoneSiteShuffle`. Samples the canyon floor on a 2 m grid; tests each candidate for: (a) flat ground within 3 m of the floor average, (b) 2.6 m clear radius + 3 m clear height above (against actual mesh colliders), (c) 10 m from Haik's statue, (d) 5 m spacing from other candidates, (e) inside map bounds. Installs results into `ZoneSiteShuffle` in the scene.

**`ZoneSiteShuffle`** (`Scripts/Mechanics/Quest/`): placed on a zone's root. A list of `Site` structs (ground point + yaw). On level load, `LevelFlowDirector` calls `ZoneSiteShuffle.ApplyAll()`, which draws three sites (at least `_minSpacing` apart) and moves each area's `AreaStation` and `Checkpoint` to the drawn spot. Draw seeded from the save, so reloading keeps the same positions.

**`LevelTemplateBuilder`** (`Scripts/Editor/`): consolidates Level 1's scene-building knowledge for Levels 2 and 3:
- **Build Template Prefabs** (run with `02_Luzon` open): saves every scene-level manager as a `Prefabs/Core/` prefab; saves `PF_ZoneTemplate` and `PF_ZoneTemplate_Final` to `Prefabs/LevelTemplate/`.
- **Create Template Scene**: `Scenes/Levels/_Template/00_LevelTemplate.unity` — three zones in a row along +Z, all managers wired, full step chain (Talk → Minigame → Report → Relic → Closing → Cleanse → Gate). Ready to duplicate.
- **Add Missing Level Pieces To Open Scene**: surgical add to an existing scene.
- **Fix Up Level IDs**: stamps `ZoneQuest.zoneId`, `QuestStep.stepId` and `Checkpoint.checkpointId` from the scene's region ID. **Critical — IDs are save keys.**
Existing prefabs are never overwritten.

**`TransitionScreen`** (`Scripts/Core/`): replaces screenshot-based crossfades. `TransitionScreen.Of(scene)` elevates all of the departing scene's root canvases (`sortingOrder = 32000`) and wraps them in `CanvasGroup`s; the system fades alpha 1→0 over the incoming scene. No GPU readback, no size mismatch, no alpha-capture problem.

**`FollowCameraInBounds`** (`Scripts/Mechanics/`): moves a particle system to `camera.position`, clamped inside a stored `Collider.bounds`. Used by zone-local ambient particles (murk, silt, marine snow). Bounds cached once in `Awake`.

---

## 5. System guide

### 5.1 Audio layers

```
MusicDirector          DontDestroyOnLoad singleton; two AudioSources crossfading.
  MusicLibrary         What each scene's BGM is; Level mode = ProximityMusic takes over.
ZoneAmbience           Lazy, in the level scene; one bed per zone, crossfade 3 s.
Sfx + SfxLibrary       Static cue set; auto-listens to ChallengeManager events.
AmbientOneShots        Fire-and-forget positional sounds.
AudioLevels            Music / SFX / Ambient / Voice mixer knobs.
UiTapSound             DontDestroyOnLoad; ButtonTap on any non-gameplay button.
```

To add a cue: add the enum entry to `SfxCue`, add an `Entry` row in `Resources/Audio/SfxLibrary`, and call `Sfx.Play(SfxCue.X)` or `Sfx.PlayAt(…)`.

### 5.2 Equipped relic lifecycle

```
Player opens inventory → InventoryModalUI → taps "Equip"
  → RelicLoadout.Equip(itemId) → RelicEquipChangedEvent
  → RelicHudButtons.Rebuild() → shows the button
Player taps button → RelicUsedEvent → minigame.OnRelicUsed()
  → RelicLoadout.StartCooldown(id, seconds) → button shows sweep
```

The Compass is passive (`HasButton = false`): `RelicHudButtons` stays hidden; the minigame subscribes to `RelicEquipChangedEvent` and updates its internal compass needle.

### 5.3 Level 2 quest chain (planned; not yet built into a scene)

| Zone | Location | Deity | Minigame | Relic reward |
|---|---|---|---|---|
| 1 | Caluya | Manunubo | Fish gathering (Radiant Conch + herd ribbon) | Moontide |
| 2 | Ajuy | Kataw Elder | Selective catching (Woven Net) | Kataw Gift (TBD) |
| 3 | Padre Burgos | Bakunawa | Relocation under the chase | Pearl of Luminescence |

Same `LevelFlowDirector` → `ZoneQuest[]` → `QuestStep[]` pattern. Zone IDs should start with `vsy_` (set by **Fix Up Level IDs**).

### 5.4 Swim stamina bar

```
Surge combo 1: +1/3 seg → combo 2: +1/6 seg → combo 3: +1/2 seg (= 1 full seg total)
7 s cooldown only starts after the 3rd surge (or after Riptide ends)
Riptide: reads Surge.Swim.FullSegments; 1 segment = 10 s boost; bar drains live.
Bar refills on checkpoint respawn.
Bar saved as [0,1] fill alongside other vitals.
```

### 5.5 Scene flow (unchanged from Oct 4)

No scene flow changes in this update. `TransitionScreen` replaces screenshot-based crossfades internally but the scene order and rules are the same. See [DETAILED_UPDATES_2026-10-04.md §5.1](file:///Users/admin/Documents/LALIM/DETAILED_UPDATES_2026-10-04.md#51-scene-flow).

---

## 6. Editor tools

New tools (run in the scene noted):

| Menu | What it does |
|---|---|
| **Tools ▸ Lalim ▸ Use Rigged Sea Creatures** | Builds `SeaCreatureClips.asset`; swaps Luzon's static creature meshes for rigged FBX; updates wanderer prefabs. Run in `02_Luzon`. |
| **Tools ▸ Lalim ▸ Add Solid Scenery Colliders** | Adds mesh colliders to solid scenery in the open scene (arches, walls, rocks, props). |
| **Tools ▸ Lalim ▸ Level 1 ▸ Find Zone 2 Sites** | Geometry-tests the zone 2 floor and installs a pool of valid minigame spots into `ZoneSiteShuffle`. Run in `02_Luzon`. |
| **Tools ▸ Lalim ▸ Level Template ▸ Build Template Prefabs** | Saves per-manager `Core/` prefabs and two `PF_ZoneTemplate` prefabs. Run with `02_Luzon` open. |
| **Tools ▸ Lalim ▸ Level Template ▸ Create Template Scene** | Creates `Scenes/Levels/_Template/00_LevelTemplate.unity` with everything placed and wired. |
| **Tools ▸ Lalim ▸ Level Template ▸ Add Missing Level Pieces To Open Scene** | Surgical add to an existing scene (e.g. `03_Visayas`). |
| **Tools ▸ Lalim ▸ Level Template ▸ Fix Up Level IDs** | Stamps zone/step/checkpoint IDs from the region ID. **Run this on every duplicated template scene before playtesting.** |

All tools from the Oct 4 doc remain and are unchanged.

---

## 7. Tests

The existing 98 EditMode tests in `Lalim.Tests` all still apply. No new test files were added in this update (Level 2 systems are too early in development).

**New testable pure-math classes that should get tests next:**
- `SwimStaminaPool` — charge rules, Riptide segment consumption, refill.
- `RelicLoadout` — equip/unequip flow, cooldown timing.
- `ZoneSiteShuffle` — seed-stable draw, spacing enforcement.

---

## 8. File inventory

> `.meta` files omitted. Paths are relative to `Assets/Lalim/` unless they start with `Assets/` or the repo root.

### 8.1 Added — scripts (new, untracked)

**Abilities/:** `SwimStaminaPool.cs`

**Core/:** `AmbientOneShots.cs`, `AudioLevels.cs`, `MusicDirector.cs`, `MusicLibrary.cs`, `PauseClock.cs`, `Sfx.cs`, `SfxLibrary.cs`, `TransitionScreen.cs`, `ZoneAmbience.cs`

**DDA/:** `Level2DifficultyKnobs.cs`

**Inventory/:** `RelicLoadout.cs`

**Mechanics/:** `AmbientSwimmers.cs`, `CruisingAnimal.cs`, `CreatureRig.cs`, `DiverIdleFloat.cs`, `FollowCameraInBounds.cs`, `KatawVillager.cs`, `ManunuboWisp.cs`, `SeaCreatureClips.cs`, `TideCurrent.cs`, `TwoBoneIK.cs`

**Mechanics/Quest/:** `Level2DialogueScripts.cs`, `SortingStation.cs`, `ZoneSiteShuffle.cs`

**Mechanics/Quest/Steps/:** `FishGatheringStep.cs`, `SchoolRelocationStep.cs`, `SelectiveCatchingStep.cs`

**Challenges/Gathering/:** `ConchCall.cs`, `Herd.cs`, `HerdRibbon.cs`, `HidingZone.cs`, `Predator.cs`, `PredatorAlertUI.cs`

**Challenges/Catching/:** `CatchableSchool.cs`, `NetBag.cs`, `NetThrower.cs`, `RootPool.cs`

**Challenges/Relocation/:** `Bakunawa.cs`, `BakunawaBody.cs`, `RelocationHud.cs`, `Sanctuary.cs`

**UI/:** `CalloutUI.cs`, `HintBulb.cs`, `InventoryModalUI.cs`, `ItemPortraits.cs`, `RelicHudButtons.cs`, `RelicIcons.cs`, `UiTapSound.cs` (+ `NoTapSound` marker)

**Editor/:** `Level1ZoneSites.cs`, `LevelTemplateBuilder.cs`, `RiggedSeaCreatures.cs`, `SolidSceneryColliders.cs`

### 8.2 Added — assets (new, untracked)

| Group | Contents |
|---|---|
| **Audio** | `Audio/Music/MUS_Intro.mp3`; `Audio/Ambient/Luzon/`, `Ambient/Visayas/`, `Ambient/Shared/`; `Audio/SFX/Luzon/`, `SFX/Visayas/`, `SFX/Shared/` |
| **Data** | `Data/Difficulty/FishGathering.asset`, `SchoolRelocation.asset`, `SelectiveCatching.asset`; `Data/Items/10_Moontide.asset`, `11_KatawGift.asset`, `12_PearlOfLuminescence.asset` |
| **Models** | `Models/Bakunawa/Bakunawa.fbx` (+ Materials/, Textures/); `Models/Kataw/` (6 FBX + Animation/ + Materials/ + Textures/); `Models/Environment/Visayas/` (8 FBX + Mangroves/ + Materials/); `Models/sea creatures + elements (fbx)/Shark.fbx`, `WhaleShark/` |
| **Prefabs** | `Prefabs/Characters/Kataw/`; `Prefabs/Core/` (per-manager prefabs); `Prefabs/Creatures/PF_Bakunawa_Model.prefab`, `PF_Wanderer_WhaleShark.prefab`; `Prefabs/LevelTemplate/` (zone template prefabs); `Prefabs/Relics/PF_Relic_Moontide_PH.prefab`, `PF_Relic_KatawGift_PH.prefab`, `PF_Relic_PearlOfLuminescence.prefab` |
| **Resources** | `Resources/SeaCreatureClips.asset`; `Resources/Audio/` (empty `MusicLibrary` and `SfxLibrary` to fill in) |
| **Scenes** | `Scenes/Levels/_Template/00_LevelTemplate.unity`; `Scenes/Levels/02_Visayas/03_Visayas.unity` (placeholder) |
| **Sprites** | `Sprites/Characters/manunubo.png`; `Sprites/UI/Game Mechanics/icon_relic_compass.png`, `icon_relic_conch.png`, `icon_relic_net.png`, `relic_button_bg.png`; `Sprites/Logos/black_cat.png` |
| **Materials** | `Materials/Visayas/` (Visayas world surface materials) |
| **Other** | `CLAUDE.md`; `1_sample.md`; `Assets/Screenshots/`; `Assets/_AxisTest/`; `Scripts/Core/graphify-out/` (probably not for the repo) |

### 8.3 Modified (selected notes)

| File(s) | Change |
|---|---|
| `Scripts/Abilities/SurgeAbility.cs` | Owns `SwimStaminaPool`; charges it per combo step; cooldown only after 3rd surge. |
| `Scripts/Abilities/RiptideAbility.cs` | Reads `SurgeAbility.Swim` for available segments; drains the bar live while running. |
| `Scripts/Abilities/BubbleBarrierAbility.cs`, `TidebondAbility.cs`, `SeagrassMendAbility.cs`, `DugongCompanion.cs`, `DugongHealth.cs`, `AbilityConfig.cs` | Minor tuning and bug fixes following the swim-bar redesign. |
| `Scripts/Core/GameConstants.cs` | Three Level 2 relic ID constants; `SWIM_STAMINA_SEGMENTS = 6`; `RIPTIDE_SECONDS_PER_SEGMENT = 10`; `TIDEBOND_COOLDOWN 30 → 7`. |
| `Scripts/Core/SaveManager.cs`, `GameEvents.cs` | `equippedRelicIds` in `SaveData`; `RelicEquipChangedEvent`, `RelicUsedEvent`, `RelicHoldEvent` events. |
| `Scripts/Core/SceneLoader.cs` | Uses `TransitionScreen` instead of screenshot crossfades. |
| `Scripts/Core/ProximityMusic.cs`, `GameAudio.cs` | Hook into `MusicDirector` for level takeover / handback; `AudioLevels` integration. |
| `Scripts/Mechanics/CameraController.cs`, `PlayerController.cs` | Adjustments for `DiverIdleFloat` and `TideCurrent` carry. |
| `Scripts/Mechanics/MarineSchool.cs` | Uses `CreatureRig.Play` for school fish. |
| `Scripts/Mechanics/PollutionDirector.cs`, `StatueDialogueRouter.cs`, `TeleportArchGate.cs`, `UnderwaterDepthFog.cs`, `BarrierNotification.cs` | Minor fixes and event updates. |
| `Scripts/Mechanics/Quest/LevelFlowDirector.cs`, `ZoneQuest.cs`, `MinigameStage.cs` | `ZoneSiteShuffle.ApplyAll()` called before player placement; `ZoneAmbience.Play` on zone change. |
| `Scripts/Mechanics/Quest/Steps/*.cs` | Minor fixes: `CleanseZoneStep`, `DialogueStep`, `LevelExitStep`, `RelicRewardStep`, `SegregationStep`. |
| `Scripts/Mechanics/Quest/AchievementService.cs`, `Level1DialogueScripts.cs` | Sfx cue calls for zone cleanse and achievement moments. |
| `Scripts/UI/*.cs` (many) | `Sfx.Play` calls replacing ad-hoc `AudioSource.PlayClipAtPoint`; `PauseClock.DeltaTime` for HUD animations; `RelicHudButtons` added to `GameHUD` layout. |
| `Scripts/Challenges/*.cs` | `Sfx.Play` calls (Correct/Wrong/ItemPickup/AltarRise) in existing minigames. |
| `Scripts/Inventory/InteractableItem.cs`, `ItemData.cs` | `worldModelPrefab` field for `ItemPortraits`; `icon` sprite for relic HUD fallback. |
| `Scripts/DDA/*.cs`, `Data/DDAConfig.asset` | `Level2DifficultyKnobs` referenced; minor DDA fixes. |
| `Scripts/Editor/AbilitySetupUtility.cs`, `EnvironmentPrefabsBuilder.cs`, `DialogueUIBuilder.cs`, `Level1SceneBuilder.cs`, `Level1RelicRewards.cs`, `Level1TrashScatter.cs` | Updated to wire SFX, `ZoneAmbience`, and `ZoneSiteShuffle`. |
| `Scripts/Editor/Tests/AbilitySystemTests.cs` | Updated for swim-bar changes (Surge cooldown rules). |
| `Prefabs/LevelFlow/PF_DialogueSystem.prefab`, `PF_QuestObjectiveUI.prefab`, `PF_Station_CoralSitePrep.prefab`, `PF_Station_GhostNet.prefab`, `PF_ToastSystem.prefab` | Updated for `Sfx` and `PauseClock`. |
| `Prefabs/Creatures/PF_Wanderer_StingRay.prefab`, `PF_Wanderer_Turtle.prefab` | `CreatureRig` added; clips assigned. |
| `Prefabs/PF_GameCamera.prefab` | Minor camera rig tweak. |
| `Prefabs/Player_final_final.prefab`, `Prefabs/UI/GameHUD.prefab` | `DiverIdleFloat` added; swim stamina bar UI (visual pending); `RelicHudButtons`. |
| `Data/AbilityConfig.asset` | `swimStaminaSegments = 6`, `riptideSecondsPerSegment = 10`, `tidebondCooldown = 7`. |
| `Data/Items/00_RadiantConch.asset`, `01_Compass.asset`, `02_WovenNetFragment.asset` | `worldModelPrefab` and `icon` sprite linked. |
| `Data/ItemDatabase.asset` | Three new Level 2 relic entries. |
| `Fonts/PlusJakartaSans-ExtraBold SDF Padded.asset`, `PlusJakartaSans-Medium SDF Padded.asset`, `Spicy Sale SDF.asset` | Font atlas updates. |
| `Materials/TrashExtracted/tripo_mat_*.mat` ×4 | URP material fixes (Emit keyword). |
| `Scenes/00_BootSequence.unity`, `Levels/01_Luzon/02_Luzon.unity`, `Tutorial/01_Tutorial.unity`, `Levels/02_Visayas/03_Visayas.unity`, `Levels/03_Mindanao/04_Mindanao.unity` | Minor scene fixes; audio sources wired; `03_Visayas` created. |
| `Settings/Lalim_URP_Renderer.asset`, `Assets/URPDefaultResources/Default_Forward_Renderer.asset` | Minor URP settings. |
| `Sprites/Logos/unity_logo.png` | Logo asset refresh. |

### 8.4 Deleted

| File | Why |
|---|---|
| `Scripts/UI/CodexModalUI.cs` | Codex modal deferred; replaced conceptually by `InventoryModalUI` (relics and tools focus). |
| `Scripts/UI/MapModalUI.cs` | Map modal deferred; no replacement yet. ⚠️ Any scene or prefab that referenced it will have a missing-script error. |
| `Sprites/Logos/koral_labs.png` | Old studio logo; replaced by `black_cat.png`. |

---

## 9. Rules & conventions

All rules from the Oct 4 document apply ([§9 there](file:///Users/admin/Documents/LALIM/DETAILED_UPDATES_2026-10-04.md#9-rules--conventions)). Additions:

13. **Audio:** always play sounds through `Sfx.Play(SfxCue.X)` or `Sfx.PlayAt(…)` for shared cues. Never `AudioSource.PlayClipAtPoint` for cues already in `SfxLibrary`. Use `AmbientOneShots.PlayAt(clip, pos)` only for unique one-off clips not worth enumerating.
14. **Music:** don't play `AudioSource.PlayOneShot` for BGM. Use `MusicDirector` (via `MusicLibrary` scene entries) so crossfades and level takeover work correctly.
15. **Swim bar:** `SurgeAbility.Swim` is the single source of truth. `RiptideAbility` and any future swim-bar UI read from it; don't cache a copy elsewhere.
16. **Equipped relics:** check `RelicLoadout.IsEquipped(id)` and react to `RelicEquipChangedEvent` and `RelicUsedEvent`. Don't control `RelicHudButtons` directly.
17. **Level 2 dialogue:** the lines in `Level2DialogueScripts` are placeholder. Don't design UI or minigame logic around specific words in them.
18. **Template scene:** after duplicating `00_LevelTemplate.unity`, run **Fix Up Level IDs** before any play-mode test. Template IDs start with `tmpl_`; they will clash with other levels' save data.
19. **`TransitionScreen`:** don't bypass it with `ScreenFader` for game-scene transitions. Call `SceneLoader.LoadScene(…)` as before.
20. **`CodexModalUI` / `MapModalUI`:** deleted. Remove any lingering references before committing to prevent missing-script errors in builds.

---

## 10. Known issues & open questions

- **`MusicLibrary` and `SfxLibrary` are empty.** No audio plays until clips are assigned. Priority: Luzon SFX (Correct/Wrong/ItemPickup in existing minigames) first, then BGM.
- **`03_Visayas.unity` is a blank placeholder.** The Visayas world models are imported but nothing is placed. The Level 2 minigame scripts are written but not wired into a scene.
- **Level 2 dialogue is all placeholder.** Real lore/writer pass needed before content review.
- **`Kataw Gift` relic has no designed ability yet.** `MOONTIDE` (reveals tide currents) and `PEARL_OF_LUMINESCENCE` (planned light-based ability) are designed; `KATAW_GIFT` is "TBD".
- **`ZoneSiteShuffle` pool not yet filled.** Run `Level1ZoneSites` in `02_Luzon` once after the current level geometry is settled to generate valid spots.
- **`SeaCreatureClips.asset` is untracked.** Run **Use Rigged Sea Creatures** in `02_Luzon` to regenerate it after any fresh clone.
- **Swim bar has no HUD visual.** `SwimStaminaPool` works; there is no `SwimStaminaBarUI` component or HUD element for it yet.
- **`MapModalUI` is deleted** and there is no replacement for the map view. Decide: defer, or build a minimal map view.
- **`graphify-out/`** in `Scripts/Core/` is a dependency-graph dump. Confirm and `.gitignore` if it shouldn't be in source control.
- **`Assets/Screenshots/`** and **`Assets/_AxisTest/`** are tracked but probably not intended for source control.
- All Oct 4 known issues still open: shader warm-up recordings for Android, Coral Seed toast ordering, tutorial crossfade not play-tested, performance watch on low-end phones. See [DETAILED_UPDATES_2026-10-04.md §10](file:///Users/admin/Documents/LALIM/DETAILED_UPDATES_2026-10-04.md#10-known-issues--open-questions).

---

## 11. Footnotes

[^tests]: The `Lalim.Tests` filter matters: unfiltered runs include URP package tests, and `EditorTests.ValidateDefaultRendererAfterReset` hangs.

[^swimbar]: `SwimStaminaPool` is pure-math and has no Unity lifecycle. Unit-test it with `new SwimStaminaPool(6)` and call `.ChargeForSurge(comboIndex)`, `.StartRiptide()`, `.DrainRiptide(dt)` directly.

[^relicloadout]: `RelicLoadout` is a static class. It reads from `SaveManager.Instance.CurrentSave`. In tests, set up a `SaveData` and a mock `SaveManager` before calling `Equip`.

[^transitionscreen]: `TransitionScreen.Of(scene)` snapshots the scene's canvases at the moment of call. Call it in the last frame before `SceneManager.LoadScene` to avoid capturing a blank canvas.

[^templatescene]: `00_LevelTemplate.unity` uses the generic region ID `tmpl`. Zone IDs are saved as `tmpl_zone1`, etc. Running **Fix Up Level IDs** after duplicating the scene stamps the new region prefix (e.g. `vsy_zone1` for Visayas).

---

*Generated 2026-10-08 from the working tree (`git status` against `c16e4fe`) and the session history. If something here disagrees with the code, the code wins — please update this file.*
