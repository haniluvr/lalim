# DETAILED UPDATES — 2026-10-04

**Branch:** `hym-test`
**Last commit (baseline):** `8e8f8d5` "a few tweaks" (2026-09-18)
**Covers:** every uncommitted change in the working tree from 2026-09-26 to 2026-10-04, in the order the work happened.
**Size (not counting `.meta` files):** 545 files added · 76 modified · 21 deleted.

This file is for teammates and for AI assistants (Claude etc.) working on Lalim. It explains **what changed, why, where it lives, and how to work with it safely**. The commit is too big for a commit message, so this document is the record. Read §1 and §2 before touching anything.

---

## Table of contents

1. [TL;DR](#1-tldr)
2. [⚠️ Read before you pull / commit](#2-️-read-before-you-pull--commit)
3. [Quick start (how to run and test)](#3-quick-start)
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

- **New skill kit + HUD (landscape only):**
  - Surge / Riptide, Spirit Sense, Tidebond (ride the dugong), Seagrass Mend, Bubble Barrier.
  - Seashell stamina orbs, HP bar, a dugong companion and its own health.
  - A "behind-the-back" third-person camera.
  - Pause modal with save slots; a full Settings modal.
- **Level 1 (02_Luzon) built end to end:** 3 zones, 3 deities (Amansinaya, Haik, Neguno).
  - **Quest system:** talk to the statue → minigames → relic → cleanse → barrier opens → tunnel exit.
  - **Five minigames:** trash collection, trash sorting at shrines, water readings, coral site prep + planting, ghost-net untangling.
  - **Supporting systems:** DDA difficulty profiles, achievements, toxic water, checkpoints, autosave/resume, pollution that recedes zone by zone.
- **Dialogue system:** NPC portraits, Story Review, Auto/Speed/Pause, and a 3/4 dialogue camera with fade in/out.
- **Scene flow:**
  - Boot → Splash → Menu → Name Input → Tutorial → Teleport hub (arch portals with seamless video) → Level → Teleport…
  - The loading screen does real staged loading (memory, scene data, shader warm-up).
  - Game scenes **crossfade** into and out of loading.
- **Underwater look:**
  - Post-processing volume, underwater sky dome and depth-texture fog.
  - Bubbles and plankton, schools of fish, all scene creatures swim, god rays, a pitch-black deep well.
- **Reusable prefabs** for Levels 2–3 (level systems, zone, stations, barrier, UI, environment, creatures).
- **Tests:** 98 EditMode tests, all passing on 2026-10-04 (group `Lalim.Tests`).

---

## 2. ⚠️ Read before you pull / commit

| # | Issue | What to do |
|---|---|---|
| 1 | `Packages/manifest.json` now has `"com.coplaydev.unity-mcp": "file:/Users/admin/Downloads/unity-mcp-beta/MCPForUnity"` — a **local path on one Mac**. | Anyone without that folder gets a package-resolve error. Remove the line before committing (it's only the Claude↔Unity bridge), or make sure each teammate has the package at that path. Two other packages were added and are needed: `com.unity.cloud.gltfast` (the `bowl.glb` model) and `com.unity.visualeffectgraph`. |
| 2 | `Assets/_Recovery/0 (1).unity`, `0 (2).unity` | Unity crash-recovery copies. Don't commit them (delete, or add to `.gitignore`). |
| 3 | `Assets/defaultMat.mat`, `Assets/Lalim/defaultMat.mat`, `Assets/Editor/CheckMeshes.cs` | Stray files from imports/debugging. Review, then probably delete. |
| 4 | `Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat` modified | URP is an **embedded** package in this project. That edit is accidental (an editor touch); revert it unless someone needs it. |
| 5 | `Assets/AI Toolkit/Temp/...png` deleted | Temporary file from the Unity AI toolkit; deleting it is fine. |
| 6 | **Save file location (macOS):** `~/Library/Application Support/DefaultCompany/Lalim_Capstone_Final/lalim_save_slot_0.json` | Pressing Play loads slot 0. To replay Level 1 from the start: **Tools ▸ Lalim ▸ Level 1 ▸ Reset Level 1 Progress**. |
| 7 | Unity preference *Script Changes While Playing* = "Recompile And Continue Playing" on the main dev machine | Saving a script while in Play mode reloads the domain mid-session. Stop Play before editing scripts. |
| 8 | Large binaries: `Videos/LoadingReef_Loop.mp4`, `Videos/PortalSwirl_Loop.mp4`, `.graphicsstate` files (~300–400 KB each), the creature FBX folders | Normal Git handles these. If your remote has file-size limits, consider Git LFS for `*.mp4`, `*.fbx` and `*.blend`. |
| 9 | Scenes were **renamed/moved** (§4.3) | Old `Scenes/Others/TeleportScene.unity` → `Scenes/Others/TeleportScene/TeleportScene.unity`. Old `02_Luzon` became `Tutorial/01_Tutorial`, and the old `02_LuzonNew` became `Levels/01_Luzon/02_Luzon`. Build Settings are updated. |

---

## 3. Quick start

**Play the real flow:** open `Scenes/00_BootSequence/00_BootSequence.unity` and press Play.
- **Order:** Boot → Splash → Main Menu → New Game → Loading → Name Input → (fade) Tutorial → Loading → Teleport → Loading → Luzon.
- **P** skips Name Input and Tutorial.

**Play Level 1 directly:** open `Scenes/Levels/01_Luzon/02_Luzon.unity` and press Play.
- `DevBootstrapper` loads the managers, and slot 0 is loaded, so you resume at your checkpoint.
- A `SceneLoader` is created on demand, so the tunnel exit still shows the loading screen.

**Run tests:** Window ▸ General ▸ Test Runner ▸ EditMode, filtered to the **`Lalim.Tests`** assembly/group.[^tests]

**Dev hotkeys** (editor and development builds, from `AbilityDebugOverlay`):

| Key | Action |
|---|---|
| F9 | Ability/vitals readout |
| F10 | Infinite stamina |
| F11 | Cycle difficulty 0 / 0.5 / 1 |

---

## 4. Timeline — earliest to latest

> Dates are when each request was worked on. Many requests were refined over several rounds; only the final behaviour is described. File paths are under `Assets/Lalim/` unless shown otherwise.

### 4.1 Skill kit, HUD, camera, pause & settings (2026-09-26 → 09-29)

**Abilities** (`Scripts/Abilities/`). Every timing, cost and range is tuned in **`Data/AbilityConfig.asset`** (`AbilityConfig.cs`). `AbilityController` routes input and enforces the rules between abilities.

| Ability | Input | Rules |
|---|---|---|
| **Surge** (basic) | tap Shift / Surge button | 8 m dash, chainable **3× as a combo**, 7 s cooldown after the 3rd or when the 1.5 s combo window lapses. **No stamina.** Blocked during Riptide and while mounted. |
| **Riptide** (basic) | hold 0.6 s | 1.75× speed for 60 s, 30 s cooldown, no stamina; hold again to cancel. Its duration shows as a thin line under the HP bar (`RiptideTimerUI`). |
| **Spirit Sense** | Q | Costs 1 orb. Guiding wisp (`SpiritWisp`) leads to the current objective for the whole duration; glowing targets (`SpiritGlow`). Merged with the old SpiritVision. |
| **Tidebond** (resonance) | — | Call and ride the dugong at 2.25× for up to 45 s of dugong fatigue; cooldown starts on dismount. |
| **Seagrass Mend** (support) | dugong card | Heals 30 HP over 3 s, free, 40 s cooldown. **Starts every level at 0%.** The card background fills in the seashell colour and turns **gold** when ready; the dugong portrait never desaturates. |
| **Bubble Barrier** (ultimate) | — | 20 s bubble that blocks toxic damage and one hit. **No stamina, no cooldown**: it charges from swimming and from using other skills. Starts the first level **half full**, then from 0 after each use. The fill sits **behind** the semi-transparent icon. The dugong tucks inside the bubble. |

**Vitals:**
- **Stamina:** `PlayerStamina` + `StaminaPool` (pure math, unit-tested). 4 seashell orbs (`sea_shell.svg`); each shell fills from the bottom. A new game or new level always starts at **2 orbs**.
- **Player HP:** `PlayerHealth` never regenerates passively. It refills only via Mend or a checkpoint respawn.
- **Dugong HP:** `DugongHealth`, 300 HP, takes ~35% of toxic damage. This is the **green bar under the dugong card**.
- **Saving:** `PlayerVitals` saves and restores HP, stamina, dugong HP and every cooldown and charge.
- **Paused during dialogue:** `GameplayClock` stops all vitals and skill timers while a dialogue is open.
- **Shared colours:** `HudPalette` holds the seashell colours used by the orbs, the Mend card and the Barrier fill.

**HUD:**
- **Layout:** landscape only. HP at bottom centre with the seashell orbs under it, skill buttons on the right, joystick on the left.
- **Prefab:** `Prefabs/UI/GameHUD.prefab` (hand-edited by the owner, see §9). Built by **Tools ▸ Lalim ▸ Abilities ▸ 4. Build Ability HUD**.
- **Button cooldowns:** each `AbilityButtonUI` shows a radial overlay plus a countdown number.

**Camera** (`CameraController`):
- **Style:** **behind-the-back**[^camera]. Horizontal look turns the diver's body (yaw) together with the camera; vertical look only tilts the camera.
- **Movement:** the joystick moves relative to the camera, and sideways input strafes.
- **Rig:** a spring arm behind and slightly above the shoulders.
- **Dugong:** swims **beside** the player and is a bit smaller.

**Pause modal** (`PauseModalUI`, `Editor/PauseModalBuilder.cs`):
- **Buttons:** Resume / Save / Exit, plus a settings icon.
- **Save:** a loaded game saves straight to its slot. A never-saved new game opens a slot picker (overwrite confirmation included).
- **Exit:** warns if the game hasn't been saved.
- **Art:** `Square Modal.png` + `rounded-btn.png`.

**Settings modal** (`SettingsModalUI`, `SettingsManager`, `GameSettings`, `GameAudio`, `StorageTools`, `HudHandedness`, `PillSwitch`):
- **Tabs:** Audio / Quality / Controls / System on `Rectangle Modal.png`, with pearl-knob slide switches.
- **Settings covered:**
  - **Audio:** 3D spatial audio, headphone optimisation.
  - **Quality:** C/B/A quality rows (texture, mapping, SSS, ambient effects via `AmbientEffect`).
  - **Controls:** gyroscope, auto-swim, right-handed mode (mirrors the HUD).
  - **System:** User Agreement / Privacy Policy (`Data/Legal/*.txt`) and resource-management buttons (clear cache, manage saves).
- **Prefab:** `Prefabs/UI/SettingsModal.prefab`, shared by the pause modal and the main menu. Close uses `rounded-btn-warning.png`.

**Removed (replaced by the above):** `MoonTideAbility.cs` (folded into Bubble Barrier), `ToolEquipSystem.cs`, `InventoryUI.cs`, `PauseMenu.cs`, `SettingsUI.cs`, plus the old `icon_moontide`, `icon_codex` and `icon_navigation` sprites.

### 4.2 Level 1 plan & build (2026-10-01 → 10-02)

Plan doc referenced in code: `LEVEL1_IMPLEMENTATION_PLAN.md`[^plan].

**Structure:** each zone follows the same loop: *toxic water → talk to the statue → minigame(s) → (relic) → cleanse → open the next barrier*.

| Zone | Deity | Minigames | Reward |
|---|---|---|---|
| 1 | Amansinaya | Trash collection (3rd person) → trash sorting (1st person) | Radiant Conch (relic) + Water Sampling Kit (tool) |
| 2 | Haik | Water readings ×3 areas → (Coral Seed tool) → coral site prep + planting ×3 areas | Compass (relic) |
| 3 | Neguno | Ghost-net untangling ×3 areas | Woven Net Fragment (relic); the tunnel is the "reward" |

**Code** (`Scripts/Mechanics/Quest/`):
- `LevelFlowDirector` runs zones in order. It handles resume: finished step IDs are restored, the first unfinished step begins, and the player is placed at the last checkpoint.
- `ZoneQuest` holds each zone's statue, toxic water, music and ordered quest steps.
- **Step types:** `TalkStep`, `TrashCollectionStep`, `SegregationStep`, `AreaMinigameStep`, `DialogueStep`, `GrantItemStep`, `RelicRewardStep`, `CleanseZoneStep`, `UnlockBarrierStep`, `LevelExitStep`.
- **Multi-area minigames:** `AreaStation` + `IAreaMinigame` + `MinigameStage` (the first-person camera stage).
- **Navigation:** `ObjectiveTracker` is the single "where next" target, read by Spirit Sense, the audio hints and `ProximityMusic`. Music gets louder near the objective and never goes silent.

**Other systems built here:**
- **Dialogue:** `StatueDialogueRouter` and `Level1DialogueScripts`, with lore researched for each deity.
- **Achievements:** `AchievementService` (Clean Sweep, Steady Hands, Net Breaker…).
- **Pollution:** `PollutionDirector` makes the level visibly cleaner as zones are cleared.
- **Toxic water:** `ToxicWaterVolume` + `ToxicWaterTracker`, with a level drain of 0.5 HP/s (`LevelToxicity`).
- **Checkpoints:** `Checkpoint` + `CheckpointManager`. Respawn refills everything.
- **DDA:** per-minigame difficulty profiles in `Data/Difficulty/*.asset` (`MinigameDifficultyProfile`, knob names in `Level1DifficultyKnobs`), plus `ExplorationMetricsTracker`.
- **Autosave:** new games autosave.

**Built by:** **Tools ▸ Lalim ▸ Level 1 ▸ Build Level Flow** (`Editor/Level1SceneBuilder.cs`). It is idempotent and surgical: it only creates what's missing.

**Dialogue UI** (`DialogueManager`, `DialogueHUD`, `ReviewOverlayUI`, `Editor/DialogueUIBuilder.cs`):
- **Box:** one long box that swaps NPC/Player art. Statue portraits (`Sprites/Characters/amansinaya|haik|neguno.png`) sit bottom-left.
- **Buttons (top-left):** Story Review, Auto (spinner when on), Speed, Pause.
  - Pause toggles to play and freezes everything (`timeScale`).
  - The buttons sit behind the review overlay.
- **Story Review:** a dark full-screen log. NPC lines start from the left, player lines from the right.

**Quest box** (`QuestObjectiveUI`, `PF_QuestObjectiveUI`): `quest-btn.png` + `quest-textbox.png`, top-left, can be toggled. Behaviour was updated on 10-04, see §4.6.

**World interaction:**
- **Floating 3D prompts:** `WorldInteractPrompt` faces the camera from any angle, and `InteractActionButton` handles taps.
- **Zone 1 trash:** re-scattered so one visible piece = one pickup, lying flat on the sand, zone 1 only (**Tools ▸ Lalim ▸ Level 1 ▸ Scatter Zone 1 Trash**).
- **Barriers:** they look like water, and their messages say "Complete Zone N to pass through." (`BarrierNotification`).

### 4.3 Scene reorganisation, prefabs, relics, teleport, loading (2026-10-02 → 10-03 morning)

**Scene moves:**
- The old `02_Luzon` (and its related files) moved to `Scenes/Tutorial/` as **`01_Tutorial`**; the legacy tutorial was deleted.
- `02_LuzonNew` was renamed to **`Scenes/Levels/01_Luzon/02_Luzon.unity`** (the final Level 1).
- `MapBounds` was fixed after manual edits.

**Reusable prefabs** (**Tools ▸ Lalim ▸ Build Reusable Level Prefabs**, `Editor/LevelPrefabBuilder.cs`), in `Prefabs/LevelFlow/`:
- `PF_LevelSystems`
- `PF_Zone`
- `PF_Station_WaterCalibration`, `PF_Station_CoralSitePrep`, `PF_Station_GhostNet`, `PF_Station_Placeholder`
- `PF_LevelExit`
- `PF_FoggyBarrier`
- `PF_DialogueSystem`, `PF_QuestObjectiveUI`, `PF_ToastSystem`

Shared UI lives in prefabs, so **edit the prefab and every scene follows** (`Editor/LevelUIPrefabs.cs`, **Tools ▸ Lalim ▸ UI ▸ Use UI Prefabs In Open Scene**).

**The `DontDestroyOnLoad` placeholder** (an early "keep these objects across scenes" stand-in) was removed without changing other behaviour. Managers now come from Boot, or from `DevBootstrapper` when you play a scene directly.

**Relics** (`Editor/RelicAssetBuilder.cs`, `Editor/Level1RelicRewards.cs`, `RelicRewardStep`, `RelicPresentation`):
- **When:** one per deity, given in a thank-you dialogue after the zone's last minigame. Needed in Level 2.
- **Items:**

  | Relic | Item asset | Prefab |
  |---|---|---|
  | Radiant Conch | `Data/Items/00_RadiantConch.asset` | `Prefabs/Relics/PF_Relic_RadiantConch.prefab` |
  | Compass | `01_Compass.asset` | `PF_Relic_Compass.prefab` |
  | Woven Net Fragment | `02_WovenNetFragment.asset` | `PF_Relic_WovenNetFragment.prefab` |

  Meshes are optimised in `Models/relics/Optimized/`.
- **Item database changes:** old placeholder items deleted (`01_WovenNet`, `02_BioluminescentPearl`, `03_TideLens`, `05_CoralReplantingKit`); `09_CoralSeed` added.

**Teleport hub** (`Scenes/Others/TeleportScene/TeleportScene.unity`):
- **Arch portals:**
  - **Video:** a seamless-loop swirl (`Videos/PortalSwirl_Loop.mp4`, made with `Tools/make_seamless_loop.swift`[^loop]). One `VideoPlayer` feeds every arch (`ArchPortalVideo`).
  - **Shape:** each portal surface is a rectangle plus semicircle mesh, placed by hand (`ArchPortalSurface`).
- **Progression** (`TeleportArchGate`):
  - Locked arches are grey, fogged and blocked.
  - Unlocked arches play in colour; swimming through teleports you.
  - Portal_0 → Luzon, Portal_1 → Visayas, Portal_2 → Mindanao.
- **Unlock moment:** `TeleportUnlockReveal` plays a one-time light burst when an arch unlocks.
- **Fixes:** the camera now uses the game camera prefab (`Prefabs/PF_GameCamera.prefab`), and the player can swim through the portal surfaces.

**Loading screen** (`Scenes/04_Loading`, `Prefabs/UI/PF_LoadingScreen.prefab`, `LoadingScreen`, `LoadingFacts`, `VideoBackground`):
- **Background:** looping video `Videos/LoadingReef_Loop.mp4` (higher-res replacement, seamless loop, with a poster frame).
- **Progress:** a % number plus a small bar.
- **Fun facts:** a random fact (deity / relic / tool / mechanic / ocean / tip) that rotates on long loads.
- ⚠️ The layout was **hand-adjusted by the owner** — don't rebuild it (§9).

**Transitions and flow** (`SceneLoader`, `ScreenFader`, `SceneSkipKey`):
- Boot fades out → Splash fades in.
- Loading **crossfades** into the next scene (never through black).
- New Game → Loading → Name Input → (fade, no loading) → Tutorial → Loading → Teleport → Loading → Luzon → Loading → Teleport (Visayas unlocked).
- **P** skips Name Input and Tutorial.

### 4.4 Level 1 polish — dialogue, trash sorting, UI order, barriers (2026-10-03)

**Dialogue framing:**
- Talking to a statue fades out, places the diver and dugong at the statue's **TalkPoint/TalkDugong**, cuts to a fixed **3/4 camera** (statue face left, diver and dugong right), then fades in. It reverses at the end.
- `StatueDialogueRouter` + `CameraController.SetShot/ClearShot` + `DugongCompanion.HoldAt`; set up by **Tools ▸ Lalim ▸ Level 1 ▸ Set Up Statue Talk Framing**.
- HP drain, stamina regen and skill timers **pause during dialogue**, and resuming a save restores the exact vitals.

**Trash sorting** (zone 1, minigame 2; `SegregationStep`, `SegregationAltar`, `OfferingItem`, `Editor/SortingShrinesBuilder.cs`):
- **Start:** begins automatically after the last trash pickup.
- **Shrines:** three `Prop_Tribal_Statue_Shrine` pillars **rise from the sand**, each topped with `Models/items/bowl.glb` and a **glowing orb** carrying the category icon (`Textures/Sorting/T_SortIcon_*`). Order left → right: **Glass, Plastic, Metal**.
- **First person:** true first-person view (the diver's body is hidden).
- **Interaction:** trash floats in a row at the bottom of the screen; drag each piece into a bowl. No counter, and no text labels on the orbs.
- **HP:** toxic drain keeps running during first-person minigames but is **floored at 1 HP**[^hpfloor].

**Toasts and banners** (`ActToastUI`, `ZoneBannerUI`, `CleanseZoneStep`):
- **ACT toast:** "ACT I – The Anomaly in the Shallows". Shown once per save, on a fresh start only (not on resume).
- **Rewards:** one **"QUEST COMPLETE / You received a …"** toast after the dialogue.
- **Order:** ACT → Quest Complete → Achievement. The zone banner always waits for toasts.
- **Removed as redundant:**
  - "Progress restored"
  - zone-unlocked banners
  - "Tap Spirit Sense…" at the start of trash collection
  - the sorting counter
  - "Luzon teleport unlocked"
  - the level-exit message
- **Kept (real warnings):** locked barrier, "You need the right tool for this", living-coral warning[^notify].

**Barriers and level geometry:**
- Zone 3 and Tunnel barriers now match Zone 2's; all are instances of **`PF_FoggyBarrier`** with per-instance messages.
- Blocking debug geometry was hidden: `Baseline_Journey_Grid`, `Layout_Magwayen_Well`, `Layout_Sunken_Cathedral_Volume` and the `UnderwaterBounds` barrier renderers.
- `VFX_Fog_Volume_TransitB` was given a real fog material (`MAT_FogVolume`).
- When the tunnel unlocks, `UnderwaterBounds.OpenShaft` splits `Floor_Barrier` around the well.

**Interaction and visuals:**
- **Interact prompts:** max 2 lines (`WorldInteractPrompt.FormatPrompt`: "Tap to use / Water Sampling Kit").
- **Station reach:** larger station radius (4 m).
- **Area markers:** now a subtle, slowly breathing glow (`AreaMarkerPulse`, `Textures/T_AreaGlow.png`, `Materials/AreaMarker.mat`).
- **God rays:** Zone 3 rays now use the Davis3D `M_GodRays` (same look as the tutorial).

### 4.5 Minigame redesigns, relic, tunnel, sky (2026-10-03 afternoon → night)

**Water readings** (zone 2, minigame 1; `WaterCalibrationMinigame`, `WaterSensorsUI`, `UI/GlassSprites`):
- **Panel:** a frosted-glass **"SEA SENSORS"** panel with three arc gauges (pH 6–9, Salinity 28–42, Temp 20–30), replacing the old sliders.
- **Play:** tap **STOP** while the sweeping needle is inside the green band; a miss locks that gauge out briefly.
- **Readings:** always "Normal" (the diagnostic analysis was removed).
- **View:** the first-person view faces the corals (view points for areas B and C were hand-set).

**Coral site prep** (zone 2, minigame 2; `CoralSitePrepMinigame`):
- **View:** a **top-down** camera over a flat stone set into the sand at each area.
  - The sites were moved to the flattest open ground nearby: A by 10 m, B by 8.5 m, C by 3.2 m.
- **Play:** **swipe** to brush away the algae (a painted mask), then **tap** the bleached coral skeletons to chisel them off, then plant the **Coral Seed**.
- **Bloom:** the main coral grows, then 8 reef pieces pop up in a spiral and sway.
- **Corals:** the level's own reef, saved as `Prefabs/Corals/PF_Reef_*` (10 prefabs). The Davis3D coral prefabs lost their colours under URP, so they aren't used.
- **Card:** a glass card with a progress bar and one dynamic hint ("Swipe to brush away the algae" / "Tap the dead corals to chisel them off" / …).

**Ghost nets** (zone 3; `Challenges/GhostNet/GhostNetMinigame`, `NetPuzzle`, `SpeciesBag`):
- **Scene nets:** levelled and lowered so they drape over their animals instead of standing like a tent (**Tools ▸ Lalim ▸ Level 1 ▸ Drape Ghost Nets**). Creatures rest on the seabed.
- **Puzzle:** a **top-down** view of one interconnected square-mesh net.
  - Ropes are round cords in the scene net's material; crossed ropes tint rust-red.
  - Border knots are fixed; the inner knots glow and can be dragged.
- **Freeing animals:** each animal lies under a hole sized to it (small 1×1, long 2×1, large 2×2). It escapes once **no rope or cord crosses the space it needs**.
- **Glass card:** lightbulb **hint** button flush left (with a count badge), progress bar and one hint line.
- **Tray:** "Freed Ocean Friends" shows a rendered **3D portrait** of each freed animal.
- **Haul:** an animated chevron **swipe-up arrow** hauls the net away.
- **Animal deal:** a level-wide shuffle bag (`SpeciesBag`) so species rarely repeat.

**Relic presentation** (`RelicPresentation`):
- **Size:** 1.2 m (bigger than before).
- **Entrance:** **fades into view** white-hot, then settles into a glow with a breathing halo, sparkles and a light.
- **Materials:** `M_RelicHalo`, `M_RelicSparkle`, `M_RelicFade`; assets so they ship in builds.

**Zone 3 tunnel (the Magwayen Well):**
- **Rocks:** the 120 `Well_*` rocks sat **behind** the terrain wall and were invisible. They were pushed in to stand 1.2–2.4 m out of the wall, with mesh colliders (**Tools ▸ Lalim ▸ Level 1 ▸ Line Well With Rocks**). Open water at least 7.8 m wide is kept all the way down.
- **`ShaftDarkness`** (on `TunnelEntrance`): fog, ambient light, sun and shaft lights fade to **pitch black** from y −40 to −105. It's restored when you swim back up.

**Zone banner** (`ZoneBannerUI`): a glass card that sizes to its message. It stays clear of the quest box measured **fully open** (via `QuestObjectiveUI.TryGetOpenScreenRect`): centred on phones, shifted right of the quest box on tablets.

**Underwater sky** (`UnderwaterSky`, `Shaders/UnderwaterSky.shader`, `Materials/M_UnderwaterSky.mat`):
- **What it is:** a dome around the camera whose horizon is the **live fog colour**.
- **Look:** a brighter surface glow and slow rays above, dark depths below.
- **Follows the water:** murky green when polluted, black in the well.
- **Why a dome:** it isn't a skybox, so ambient lighting is untouched.

### 4.6 Loading pipeline, environment, depth fog, quest box (2026-10-04)

**Tunnel → Teleport:**
- **Trigger:** the exit trigger covers the lower quarter of the well, **from 75% of the way down** (y −96.4 to −114).
- **Transition:** reaching it crossfades the dark frame into the loading screen, then into TeleportScene.
- **Edge cases:** it also fires if you're already inside the trigger. `SceneLoader.Ensure()` creates a loader when you started the scene directly.

**Real loading work** (`SceneLoader`, `LoadingStages`, `ShaderWarmup`):

| Stage | Bar | What happens |
|---|---|---|
| Free memory | 0–10% | Loading the loading scene alone unloads the old scene and its unused assets, then `GC.Collect()`. |
| Load & unpack | 10–80% | `LoadSceneAsync` reads, decompresses and deserialises the next scene. Background loading priority is High and the GPU upload time slice is raised while the loading screen is up. |
| Compile shaders | 80–100% | `GraphicsStateCollection` warm-up from `Resources/ShaderWarmup/<scene>_<API>.graphicsstate`[^warmup]. Skipped if there's no recording. |
| Set up scene | after 100% | Activation (`Awake`/`Start`, save restore, player placement, GPU uploads) behind the frozen loading frame. |

These stages run for **every** loading-screen transition. The Splash does the same at boot.

**Crossfades** (`SceneLoader.IsGameScene`):
- **Into loading:** leaving any **game scene** (Tutorial, TeleportScene, region levels) **crossfades into** the loading screen. Menu, Name Input and Boot still fade through black.
- **Out of loading:** loading always crossfades out.

**Save incident (fixed):** a play-mode test of the exit routine wrote to the real save, marking `luzon_exit` done and unlocking Visayas. That made the exit trigger do nothing.
- **Repair:** the save was fixed; the original is kept as `lalim_save_slot_0_before_exit_fix.json` in the save folder.
- **Rule now:** back up the save before play-mode tests (§9).

**Environment prefabs** (**Tools ▸ Lalim ▸ Environment ▸ Build Environment Prefabs**; **Tools ▸ Lalim ▸ Level 1 ▸ Add Environment To Luzon**; `Editor/EnvironmentPrefabsBuilder.cs`):
- **`Prefabs/Environment/PF_PlanktonParticles`:** plankton specks plus **rising bubbles** (`Textures/Environment/T_Bubble.png`, `Materials/Environment/M_Bubbles`, `M_Plankton`), following the camera (`FollowMainCamera`).
- **`Prefabs/Environment/PF_UnderwaterEffectsVolume`:** the post-processing profile `VP_Underwater` (bloom, colour adjustments, white balance, dark-blue vignette), the **underwater sky** and the **depth-texture fog**. Luzon's old empty volume was removed.
- **`Prefabs/Creatures/PF_School_*`:** ClownFish, BlueTang, ReefFish, Tuna (`MarineSchool`: boids-lite, scatter from the diver, avoid rocks and seabed).
- **`Prefabs/Creatures/PF_Wanderer_*`:** Turtle, StingRay (`WanderingMarineAnimal`). In Luzon, 2 schools and 1 wanderer sit near each statue under `[MarineLife]`.
- **Scene creatures:**
  - **All 31 `LuzonScene/Creature_*`** props (dolphins, whales, sharks, squid, eels, octopus, fish, turtles, rays) now swim, keeping their pose and size (posed mode, `headAxis`).
  - The heads were verified per species.
  - Animals trapped in the ghost nets stay still.
- **Model facing:** the sea-creature FBX models face **−Z**; the prefabs turn them 180°.

**Depth-texture fog** (`Shaders/UnderwaterDepthFog.shader`, `UnderwaterDepthFog.cs`, `Materials/Environment/M_UnderwaterDepthFog`):
- **How it runs:** a URP **Full Screen Pass** (before post-processing, needs depth) added to **both** renderers, `Settings/Lalim_URP_Renderer` and `URPDefaultResources/Default_Forward_Renderer`.
- **What it does:** per-channel absorption over distance (red, then green, then blue), blending into the live fog colour.
- **Where it's active:** only in scenes containing `PF_UnderwaterEffectsVolume`.

**Quest box** (`QuestStep._boxOpen`, `QuestObjectiveUI.SetObjective(text, open)`):
- **Text:** every step's text now shows.
- **Collapsed by default** (the player can still open it):
  - **Travel and return steps:** Talk 01 in each zone, the RelicGift "Return to …" steps (and the steps after them that keep that text), and 07 "Swim down the deep path".
  - **Defaults for new steps:** new `TalkStep`, `RelicRewardStep` and `LevelExitStep` components start collapsed (`DefaultBoxOpen`).
- **Open by default:** actual quests.

---

## 5. System guide

### 5.1 Scene flow

```
00_BootSequence ─fade─▶ 01_Splash ─crossfade─▶ 02_MainMenu
02_MainMenu ─fade─▶ 04_Loading ─xfade─▶ 03_NameInput ─fade (no loading)─▶ 01_Tutorial
01_Tutorial ─xfade─▶ 04_Loading ─xfade─▶ TeleportScene ─xfade─▶ 04_Loading ─xfade─▶ 02_Luzon
02_Luzon (tunnel 75%) ─xfade─▶ 04_Loading ─xfade─▶ TeleportScene (Visayas arch unlocked)
```

- **Entry point:** all loads go through `SceneLoader.LoadScene(scene, showLoadingScreen, crossfadeIn)`, or the `SceneLoadRequestedEvent` event.
- **Fallback:** direct `SceneManager.LoadScene` calls are only fallbacks for when no loader exists.
- **Game scenes:** `SceneLoader.IsGameScene()` defines which scenes these are.
- **Region gating:** `GameManager.IsRegionAccessible`.

### 5.2 Quest system (Level 1; reuse for Levels 2–3)

- **Order of operations:** `LevelFlowDirector` → `ZoneQuest[]` → `QuestStep[]`.
  - Each step has a stable **`StepId`**, saved in `SaveData.completedChallengeIds`.
  - On load, finished steps run `RestoreCompleted()` and the first unfinished step runs `Begin()`.
- **Adding a minigame:** implement `IAreaMinigame` (`Play`, `Stop`, `ShowCompletedState`, `TaskCount`, DDA baselines). Put it on an `AreaStation` under an `AreaMinigameStep`, and use `MinigameStage` for the first-person camera.
- **Reporting:** progress and errors go through `ChallengeManager` (`ReportTaskCompleted`, `ReportCorrectAction`, `UseHint`).
- **Navigation:** set `ObjectiveTracker.Instance.SetObjective(transform)`. Spirit Sense, music and audio hints follow it.

### 5.3 UI layers (top of screen)

| Element | Purpose | Notes |
|---|---|---|
| `ActToastUI` | ACT / Quest Complete / Achievement | Priority queue; `IsBusy` makes the banner wait. |
| `ZoneBannerUI` | Warnings only | Glass card; avoids the quest box. |
| `QuestObjectiveUI` | Quest box + key button | Per-step open/closed default. |
| Minigame cards | Glass instruction cards | Built at runtime from `GlassSprites` (no art needed). |

### 5.4 Rendering stack (underwater)

| Layer | Where | Notes |
|---|---|---|
| RenderSettings fog | Scene (linear 0–50 m, deep blue) | `PollutionDirector` tints it; `ShaftDarkness` blacks it out. |
| Underwater sky dome | `PF_UnderwaterEffectsVolume/UnderwaterSky` | Horizon = fog colour. |
| Depth fog pass | URP renderers + `UnderwaterDepthFog` | Per-channel absorption. |
| Post-processing | `VP_Underwater` (priority 1) | `PollutionDirector`'s polluted volume (priority 50) blends on top. |
| Ambience | Bubbles/plankton, god rays, caustics | `AmbientEffect` lets low-quality settings switch them off. |

### 5.5 Saves

- **Format and location:** JSON in `persistentDataPath` (`SaveManager`), 3 slots.
- **Session origin:** NewGame, LoadedGame or Unknown.
  - `AutoSave()` only writes when the session is bound to a slot.
  - **`SaveGame()` always writes**[^save].
- **Contents:** step IDs, vitals, inventory, relics, DDA, tutorial flags (e.g. `seen_act_*`, `seen_unlock_*`), the ghost-net species bag and the last checkpoint.

---

## 6. Editor tools

All are idempotent unless noted. Run them in the scene they target.

| Menu | What it does |
|---|---|
| **Tools ▸ Lalim ▸ Abilities ▸ 1–7 / Run All** | Icons, player prefab, dugong/checkpoint/toxic prefabs, ability HUD, region scene setup, pause modal, settings modal. |
| **Tools ▸ Lalim ▸ Level 1 ▸ Build Level Flow** | Installs or updates the whole Level 1 flow in 02_Luzon (surgical). |
| ▸ Add Relic Rewards | Inserts the RelicGift steps and their look. |
| ▸ Build Sorting Shrines | Bowls, orbs and icons on the 3 shrines. |
| ▸ Set Up Statue Talk Framing / Reframe Statue Talk Cameras | TalkPoint, TalkDugong and the 3/4 TalkCamera per statue. |
| ▸ Scatter Zone 1 Trash | One-piece-one-pickup trash on the sand. |
| ▸ Drape Ghost Nets | Lowers and levels the scene nets over their animals. |
| ▸ Line Well With Rocks | Pushes the well rocks out of the terrain wall (+ colliders). |
| ▸ Add Environment To Luzon | Bubbles, effects volume, marine life, swimming scene creatures. |
| ▸ Reset Level 1 Progress (save slot 0) | Replay Level 1 from the start. |
| **Tools ▸ Lalim ▸ Environment ▸ Build Environment Prefabs** | Creates/upgrades the environment and creature prefabs, the depth-fog pass and materials. |
| ▸ Add / Remove Mesh Colliders (Selected & Children) | Collider helpers. |
| **Tools ▸ Lalim ▸ Build Reusable Level Prefabs** | Saves the Level 1 building blocks to `Prefabs/LevelFlow/`. |
| **Tools ▸ Lalim ▸ Relics ▸ Build Relic Assets / Rebuild Relic Meshes** | Relic items, prefabs and optimised meshes. |
| **Tools ▸ Lalim ▸ Teleport ▸ Add Arch Portal Video / Set Up Arch Gates** | Portal surfaces and video; locked/unlocked gates. |
| **Tools ▸ Lalim ▸ UI ▸ Build Loading Screen / Rebuild Loading Screen Prefab** | ⚠️ Rebuild overwrites hand edits. |
| ▸ Rebuild Quest Box Prefab / Use UI Prefabs In Open Scene | Shared UI prefabs. |
| **Tools ▸ Lalim ▸ Shader Warm-up ▸ Record While Playing / Fly Camera Tour (Play mode)** | Record shader warm-up data[^warmup]. |
| Older tools: Setup Scene Managers, Create Water Sampling/Coral Replanting Zone, Validate & Fix Zone IDs, Lalim ▸ Setup Hub / Teleport Scene / Underwater Lighting / Fix Oceanfloor Materials | Pre-existing helpers; most are superseded by the Level 1 builders. |

---

## 7. Tests

**Run:** EditMode, group **`Lalim.Tests`**, 98 tests (81 `[Test]` methods, some parameterised).

**Suites:**
- `AbilityConfigBlendTests`, `SkillEconomyTests`, `StaminaPoolTests`, `ToxicDamageTests`, `LevelToxicityTests`
- `DDAConfigTests`, `DdaScoringTests`, `MinigameProfileTests`
- `GameSettingsTests`, `SaveDataTests`, `ProximityMusicTests`
- `Level1DialogueTests`, `RelicRewardTests`, `ToastTests`, `InteractPromptTests`
- `WaterReadingTests`, `NetPuzzleTests`, `SpeciesBagTests`
- `LoadingScreenTests` (fun facts plus loading-stage ordering)

---

## 8. File inventory

> `.meta` files are omitted. Paths are relative to `Assets/Lalim/` unless they start with `Assets/`, `Packages/`, `ProjectSettings/` or `Tools/`.

### 8.1 Added — scripts (by folder)

**Abilities/:**
- **Framework:** `AbilityBase`, `AbilityConfig`, `AbilityController`
- **Abilities:** `BubbleBarrierAbility`, `RiptideAbility`, `SeagrassMendAbility`, `SurgeAbility`, `TidebondAbility`
- **Spirit Sense visuals:** `SpiritGlow`, `SpiritWisp`
- **Checkpoints and hazards:** `Checkpoint`, `CheckpointManager`, `ContactHazard`
- **Dugong:** `DugongCompanion`, `DugongHealth`
- **Vitals and clock:** `GameplayClock`, `PlayerHealth`, `PlayerStamina`, `PlayerVitals`, `StaminaPool`
- **Toxic water:** `LevelToxicity`, `ToxicWaterTracker`, `ToxicWaterVolume`

**Challenges/:**
- **Zone 2 minigames:** `CoralSitePrepMinigame`, `WaterCalibrationMinigame`, `WaterSensorsUI`
- **Trash sorting:** `OfferingItem`, `SegregationAltar`
- **GhostNet/:** `GhostNetMinigame`, `NetPuzzle`, `SpeciesBag`

**Core/:**
- **Dev tools:** `AbilityDebugOverlay`, `DevBootstrapper`
- **Audio and settings:** `AmbientEffect`, `AudioChannelSource`, `GameAudio`, `GameSettings`, `ProximityMusic`, `StorageTools`
- **Loading and scene flow:** `LoadingStages`, `SceneSkipKey`, `ShaderWarmup`

**DDA/:** `ExplorationMetricsTracker`, `Level1DifficultyKnobs`, `MinigameDifficultyProfile`

**Mechanics/:**
- **Teleport hub:** `ArchPortalSurface`, `ArchPortalVideo`, `RegionTeleporter`, `TeleportArchGate`, `TeleportUnlockReveal`
- **Dialogue and statues:** `DialogueCameraPanner`, `DialogueTrigger`, `StatueDialogueRouter`
- **Interaction and navigation:** `BarrierNotification`, `IInteractionIcon`, `ObjectiveTracker`
- **Environment and rendering:** `CausticsAnimator`, `PollutionDirector`, `ShaftDarkness`, `UnderwaterDepthFog`, `UnderwaterSky`
- **Helpers:** `FaceCamera`, `FollowMainCamera`
- **Marine life:** `MarineSchool` (+ `SwimBody` helper), `WanderingMarineAnimal`

**Mechanics/Quest/:**
- **Flow:** `LevelFlowDirector`, `ZoneQuest`, `QuestStep`
- **Stations and minigame staging:** `AreaStation`, `AreaMarkerPulse`, `IAreaMinigame`, `MinigameStage`, `PlaceholderAreaMinigame`
- **Story and rewards:** `AchievementService`, `Level1DialogueScripts`, `RelicPresentation`
- **Exit:** `LevelExitTrigger`
- **Steps/:** `AreaMinigameStep`, `CleanseZoneStep`, `DialogueStep`, `GrantItemStep`, `LevelExitStep`, `RelicRewardStep`, `SegregationStep`, `TalkStep`, `TrashCollectionStep`, `UnlockBarrierStep`

**UI/:**
- **Skill and vitals HUD:** `AbilityButtonUI`, `DugongCardButton`, `HealthBarUI`, `HudHandedness`, `HudPalette`, `RiptideTimerUI`, `StaminaOrbsUI`, `SurgeRiptideButton`, `ToxicScreenEffect`, `UltimateButtonUI`
- **Dialogue:** `DialogueHUD`, `DialogueManager`, `ReviewOverlayUI`
- **Top-of-screen notices:** `ActToastUI`, `LevelIntroUI`, `QuestObjectiveUI`, `ZoneBannerUI`
- **Interaction:** `InteractActionButton`, `WorldInteractPrompt`
- **Menus and modals:** `PauseModalUI`, `PillSwitch`, `SettingsModalUI`
- **Loading and splash:** `LoadingFacts`, `SplashScreenUI`, `VideoBackground`
- **Helpers:** `FitInsideParent`, `FloatingUI`, `GlassSprites`

**Editor/:**
- **Ability and UI builders:** `AbilitySetupUtility`, `DialogueUIBuilder`, `LevelUIPrefabs`, `LoadingScreenBuilder`, `PauseModalBuilder`, `SettingsModalBuilder`
- **Level 1 builders:** `Level1RelicRewards`, `Level1SaveTools`, `Level1SceneBuilder`, `Level1TrashScatter`, `LevelPrefabBuilder`, `RelicAssetBuilder`, `SortingShrinesBuilder`, `StatueTalkFraming`
- **Environment and rendering:** `EnvironmentPrefabsBuilder`, `ShaderWarmupRecorder`, `UnderwaterLightingSetup`, `OceanfloorMaterialFixer`
- **Teleport hub:** `HubSetupUtility`, `TeleportArchPortals`, `TeleportSceneSetupUtility`
- **Tests/:** `AbilitySystemTests`, `DifficultyUpgradeTests`, `GhostNetTests`, `InteractPromptTests`, `Level1FlowTests`, `LoadingScreenTests`, `RelicRewardTests`, `SettingsTests`, `ToastTests`

**Shaders/:** `BubbleRim`, `PortalVideo`, `UIAlwaysOnTop`, `UnderwaterDepthFog`, `UnderwaterSky`

**Other:**
- `Tools/make_seamless_loop.swift` (outside `Assets/`)
- `Assets/Editor/CheckMeshes.cs` (stray, see §2)

### 8.2 Added — assets

| Group | Contents |
|---|---|
| **Data** | `AbilityConfig.asset`; `Difficulty/` (CoralSitePrep, GhostNet, Sorting, TrashCollection, WaterCalibration); `Items/01_Compass`, `02_WovenNetFragment`, `09_CoralSeed`; `Legal/PrivacyPolicy.txt`, `UserAgreement.txt` |
| **Prefabs** | `Characters/PF_Dugong`; `Corals/PF_Reef_*` ×10; `Creatures/PF_School_*` ×4, `PF_Wanderer_*` ×2; `Environment/PF_PlanktonParticles`, `PF_UnderwaterEffectsVolume`, `VP_Underwater.asset`; `Hazards/PF_Checkpoint`, `PF_ToxicWaterVolume`; `LevelFlow/*` ×11; `Relics/PF_Relic_*` ×3; `UI/PF_LoadingScreen`, `SettingsModal`; `PF_GameCamera`, `Player_final_final` |
| **Scenes** | `Others/TeleportScene/` (scene, lighting, volume profile); `Tutorial/01_Tutorial/` (lighting, volume profile) |
| **Materials** | Area marker, foggy barrier, fog volume, bubble barrier, coral algae, net knot/tangled, relic halo/sparkle/fade, underwater sky; `Environment/` (bubbles, plankton, depth fog); `Relics/` ×3; `Sorting/` (halo/icon/orb ×3 each); `Trash/` URP materials; `LuzonSceneExtracted/`, `TrashExtracted/` |
| **Models** | `Environment/LuzonScene.fbx` (the Level 1 world), `Oceanfloor/`, `Castle/`; `sea creatures + elements (fbx)` and `(blend)`; `sea god statues/luzon/` (amansinaya, haik, neguno); `relics/` (+ `Optimized/`); `tools/` (coral seed, water sampling kit); `trash/`; `items/bowl.glb`; `nets/square-net.fbx`, `triangle-net.obj`; `low-poly-ship-wreck/` |
| **Fonts** | PlusJakartaSans family SDF assets, including **"Padded"** variants[^fonts]; `la-rezolt-trial` |
| **Sprites** | `Characters/` (deities, dugong); `UI/Dialogue Boxes/` (NPC/Player boxes, quest-btn, quest-textbox, review, auto, speed, play); `UI/Game Mechanics/` (skill icons, sea_shell.svg, chat/interact); `UI/Settings/` (pearl, capsule); `UI/Buttons/` (secondary, warning); `UI/Loading*/`; `UI/Placeholders/`; `Backgrounds/SplashBackground.jpg`; `Logos/` |
| **Textures** | `T_AreaGlow.png`; `Sorting/T_SortIcon_{Glass,Plastic,Metal}.png`, `T_SortHalo.png`; `Environment/T_Bubble.png` |
| **Videos** | `LoadingReef_Loop.mp4` + poster; `PortalSwirl_Loop.mp4`; `ArchPortal/` materials |
| **Resources** | `ShaderWarmup/02_Luzon_Metal.graphicsstate`, `TeleportScene_Metal.graphicsstate`; `WorldPrompt/` materials |
| **Settings** | `UnderwaterProfile.asset` |

### 8.3 Modified (selected notes)

| File(s) | Change |
|---|---|
| `Scenes/*` (Boot, Splash, MainMenu, NameInput, Loading, Tutorial, `Levels/01_Luzon/02_Luzon`) | Flow, UI prefabs, Level 1 content (02_Luzon carries most of §4). |
| `Prefabs/UI/GameHUD.prefab` | New skill HUD, pause modal, settings (**owner hand-edited**). |
| `Scripts/Core/SceneLoader.cs` | Staged loading, crossfades, `Ensure()`, `IsGameScene()`. |
| `Scripts/Core/SaveManager.cs`, `GameManager.cs`, `RegionProgressTracker.cs`, `GameConstants.cs`, `GameEvents.cs`, `SettingsManager.cs` | Save slots/origin, vitals, flags, region unlocks, new events and constants, settings. |
| `Scripts/Mechanics/CameraController.cs` | Behind-back camera, dialogue shots, first-person body hiding, `AimFirstPersonAt`. |
| `Scripts/Mechanics/PlayerController.cs`, `InputManager.cs`, `SpiritSense.cs`, `UnderwaterBounds.cs`, `ChallengeManager.cs`, `AudioNavigationManager.cs`, `HiddenObject.cs`, `ZoneTrigger.cs`, `ZoneRestorationController.cs` | Abilities and interaction, Spirit Sense rework, `OpenShaft`, first-person challenges. |
| `Scripts/UI/*` (HUDController, LoadingScreen, MainMenuUI, NameInputUI, ScreenFader, BootSequenceUI, CodexModalUI, MapModalUI) | HUD suppression in minigames, loading facts, flow, crossfade capture. |
| `Scripts/DDA/*`, `Data/DDAConfig.asset` | Exploration metrics, profile blending, scoring. |
| `Scripts/Challenges/*` (legacy coral/water/waste challenge scripts) | Kept for older scenes and tests; Level 1 uses the new minigames. |
| `Scripts/Inventory/*`, `Data/ItemDatabase.asset`, `Items/00_RadiantConch`, `07_WaterSampleKit` | Relics vs tools, new items. |
| `Settings/Lalim_URP_Asset.asset`, `Lalim_URP_Renderer.asset`, `Assets/URPDefaultResources/Default_Forward_Renderer.asset` | URP settings; **depth-fog Full Screen Pass** added to both renderers. |
| `Assets/DefaultVolumeProfile.asset`, `Davis3D/.../M_GodRays.mat` and other Davis3D materials | URP conversion and god-ray look. |
| `Animations/DugongAnimator.controller`, `Materials/Player.mat`, `Fonts/Spicy Sale SDF.asset` | Dugong states, player material, font atlas. |
| `ProjectSettings/EditorBuildSettings.asset` | Scene list: Boot, Splash, MainMenu, Loading, NameInput, Tutorial, TeleportScene, Luzon. |
| `ProjectSettings/TagManager.asset` | Tags `ToxicWater`, `Checkpoint`, `Rocks`; layer `Hazard`. |
| `Packages/manifest.json` (+ lock) | gltfast, VFX Graph, **local MCP package (see §2)**. |
| `ProjectSettings/UnityConnectSettings`, `VFXManager`, `Packages/com.unity.ai.assistant/Settings.json` | Editor and package settings churn. |

### 8.4 Deleted

| File | Why |
|---|---|
| `Scripts/Mechanics/MoonTideAbility.cs`, `ToolEquipSystem.cs` | Replaced by the new skill kit (Bubble Barrier etc.). |
| `Scripts/UI/InventoryUI.cs`, `PauseMenu.cs`, `SettingsUI.cs` | Replaced by `PauseModalUI` / `SettingsModalUI` (inventory UI to be redone later). |
| `Data/Items/01_WovenNet`, `02_BioluminescentPearl`, `03_TideLens`, `05_CoralReplantingKit` | Placeholder items replaced by the relics and the Coral Seed. |
| `Prefabs/Player.prefab` | Replaced by `Player_final_final.prefab`. |
| `Prefabs/Environment/fantasy+castle+3d+model.fbx` (+ texture) | Moved to `Models/Environment/Castle/`. |
| `Scenes/Others/TeleportScene.unity` | Moved to `Scenes/Others/TeleportScene/`. |
| `Scenes/Levels/01_Luzon/02_Luzon/LightingData.asset`, `ReflectionProbe-*.exr`, `02_LuzonSettings.lighting` | Old baked data of the scene that became the tutorial. |
| `Sprites/UI/Game Mechanics/icon_codex.png`, `icon_moontide.png`, `icon_navigation.png` | Unused icons. |
| `Assets/AI Toolkit/Temp/...png` | Temporary file. |

---

## 9. Rules & conventions

**For AI assistants (and humans) working in this repo:**

1. **Don't rebuild what the owner hand-edits.** `GameHUD.prefab`, `PF_LoadingScreen`, `04_Loading`, `PauseModal` labels (e.g. "Main Menu" was renamed "Exit") and dialogue prefabs are adjusted by hand. Builders must be **surgical**: look up by name, create only what's missing, never overwrite.
2. **Tests:** run EditMode with **group `Lalim.Tests` only**. An unfiltered run pulls in ~570 URP package tests, and one of them hangs. The test runner also saves the open dirty scene.
3. **Play-mode testing touches the real save.**
   - Copy `lalim_save_slot_0.json` and its `_backup` first, and restore them afterwards.
   - Setting `SaveManager.Origin = Unknown` only blocks `AutoSave()`; `SaveGame()` and step completion still write.
   - Never run a step's completion code (exit, cleanse…) against the real save without a backup.
4. **Don't stop someone's active Play session.** On the main machine, scripts recompile during play, so stop Play before saving `.cs` files.
5. **Camera:** keep the **behind-the-back** style[^camera]; don't switch to free orbit without asking.
6. **Notifications:** no banners or toasts for things the player can already see; rewards go in the one Quest Complete toast after dialogue; order is ACT → Quest Complete → Achievement[^notify].
7. **Scene flow:** a loading screen never fades to black at the end. Leaving game scenes crossfades into loading. Name Input → Tutorial has no loading screen.
8. **URP gotchas:**
   - **Render order:** URP re-validates materials, so set draw order with `_QueueOffset` and blend with `_Blend` (2 = additive), not `renderQueue` alone.
   - **Emission:** the Lit material's `_EMISSION` keyword can be stripped on save. Use **Unlit** for things that must glow (net knots).
   - **Sprites/Default:** don't use it for 3D. Use URP Unlit (alpha clip) or `URP/Particles/Unlit`.
   - **Shaders in builds:** keep runtime-created materials backed by **material assets** so their shader variants ship in builds.
9. **TextMeshPro:** use the **"Padded" PlusJakartaSans SDF** assets for runtime UI[^fonts].
10. **Models:** the sea-creature FBX models face **−Z**; rotate them 180° in prefabs. The scene's own creatures use `WanderingMarineAnimal.headAxis` (the positive end of the longest axis = head).
11. **Reusable for Levels 2/3:** the `Prefabs/LevelFlow/*` and `Prefabs/Environment/*` prefabs and the quest step types. Copy the Level 1 pattern (see `Level1SceneBuilder`) instead of hand-wiring.
12. **Language:** player-facing text is short and plain (glass cards show one hint line).

---

## 10. Known issues & open questions

- **Shader warm-up recordings exist only for Metal** (Mac editor and iOS). Android (Vulkan/GLES) needs recordings from a development build on a device; it's sent to the editor automatically when connected. There are no recordings yet for Main Menu, Tutorial or Name Input, so those loads skip the shader stage.
- **Coral Seed** shows its own "TOOL RECEIVED" toast mid-zone instead of joining Quest Complete. This needs the owner's decision.
- **HP in first-person minigames** is floored at 1 instead of letting the player fail. Fail/restart is an option.
- **Tutorial → Teleport crossfade** was implemented through the shared loader rule but wasn't play-tested specifically.
- **Unused difficulty knobs:** `KnotsPerTangle` (ghost net, since the one-net redesign) and `HoldSeconds` (water readings, since the gauges). Both are documented as unused in `Level1DifficultyKnobs`.
- **Menu-side transitions** (Main Menu → loading, Name Input → Tutorial, Boot → Splash) still fade through black, by current design.
- **Performance to watch on low-end phones:** the depth-fog pass, about 70 swimming skinned meshes in Luzon, and bubbles/plankton. `AmbientEffect` covers some of these.
- **Inventory UI** was deferred ("fix later").
- Some **older tools/scripts** (Create Water Sampling/Coral Replanting Zone, legacy challenge scripts) are superseded but kept for older scenes and tests.
- **Water_Area_C**'s first-person view faces kelp rather than coral; its spot could move.

---

## 11. Footnotes

[^tests]: The `Lalim.Tests` filter matters: unfiltered runs include URP package tests, and `EditorTests.ValidateDefaultRendererAfterReset` hangs.

[^camera]: Behind-the-back camera spec (agreed 2026-09-29): horizontal look rotates the body yaw with the camera; vertical look only tilts the camera (clamped); the joystick is camera-relative, with diagonal and sideways input strafing; a spring arm sits behind and slightly above the shoulders. `CameraController._style = BehindBack` (FreeOrbit stays only as an Inspector option).

[^plan]: The Level 1 implementation plan (zones, flow, lore research, DDA knobs, achievements) was written on 2026-10-01 and is referenced by `Level1DialogueScripts`. If it isn't in the repo, ask the owner for it.

[^loop]: `make_seamless_loop.swift` builds a seamless loop by crossfading the clip's end into its start, so a video loops forward without reversing. Usage is in the script header.

[^hpfloor]: `ToxicWaterTracker`: during a first-person challenge, drain continues but HP can't drop below 1, so a minigame can't kill the player mid-puzzle.

[^notify]: User rule (2026-10-03): banners only for real warnings; ZoneBannerUI always yields to ActToastUI.

[^warmup]: Shader warm-up uses Unity 6's `GraphicsStateCollection`. The legacy `ShaderVariantCollection` tracking doesn't see URP draws. Recordings are per graphics API and live in `Resources/ShaderWarmup/<scene>_<API>.graphicsstate`. They're loaded **before** the scene's async load, because Unity holds other async loads back while a scene waits for activation.

[^save]: Found 2026-10-04 when a test wrote to the real save. Any code path that calls `SaveGame()` (step completion, level complete, teleport unlock flags) writes regardless of the session origin.

[^fonts]: The regular PlusJakartaSans SDF assets showed dark boxes behind glyphs at runtime; the "Padded" variants fix that.

---

*Generated 2026-10-04 from the working tree (`git status` against `8e8f8d5`) and the session history. If something here disagrees with the code, the code wins — please update this file.*
