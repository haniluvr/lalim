<p align="center">
  <img src="Assets/Lalim/Sprites/Logos/black_cat.png" width="80" alt="Studio Logo"/>
</p>

<h1 align="center">LALIM</h1>
<h3 align="center">An underwater educational adventure game built in Unity</h3>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6000.x-black?logo=unity" alt="Unity Version"/>
  <img src="https://img.shields.io/badge/C%23-12.0-239120?logo=csharp" alt="C# Version"/>
  <img src="https://img.shields.io/badge/URP-17.x-blue" alt="URP"/>
  <img src="https://img.shields.io/badge/Branch-hym--test-orange" alt="Branch"/>
  <img src="https://img.shields.io/badge/Platform-iOS%20%7C%20Android%20%7C%20Editor-lightgrey" alt="Platforms"/>
</p>

---

## Table of Contents

1. [What is Lalim?](#1-what-is-lalim)
2. [Lore & Story](#2-lore--story)
3. [Levels at a Glance](#3-levels-at-a-glance)
4. [Getting Started](#4-getting-started)
5. [Project Structure](#5-project-structure)
6. [Core Systems](#6-core-systems)
7. [Gameplay Mechanics](#7-gameplay-mechanics)
8. [Editor Tools](#8-editor-tools)
9. [Tests](#9-tests)
10. [Documentation & Change Log](#10-documentation--change-log)
11. [Known Issues](#11-known-issues)
12. [Contributing](#12-contributing)
13. [License](#13-license)

---

## 1. What is Lalim?

**Lalim** (Tagalog: *depth*) is a third-person underwater exploration and educational adventure game set in the seas of the Philippine archipelago. The player dives as a young Filipino diver guided by sea deities to heal three increasingly polluted ocean zones — **Luzon**, **Visayas**, and **Mindanao**.

Each level is a self-contained underwater world filled with ambient sea life, a unique deity, culture-rooted minigames, and a relic reward. The game is designed as a **capstone project**, blending marine biology education, Philippine mythology, and adaptive game design (DDA).

### Tech Stack

| Concern | Choice |
|---|---|
| Engine | Unity 6000.x (Unity 6 LTS) |
| Render Pipeline | Universal Render Pipeline (URP 17.x) |
| Language | C# 12 |
| Version Control | Git + Git LFS (large binaries) |
| Target Platforms | iOS, Android, macOS Editor |
| Audio | Unity Audio Mixer (4 channels: Music, SFX, Ambient, Voice) |
| Physics | Unity PhysX (3D) |
| AI/DDA | Custom `ChallengeManager` + per-minigame `MinigameDifficultyProfile` |

---

## 2. Lore & Story

The oceans around the Philippine archipelago are sick. Pollution, overfishing, and human carelessness have spread a deep toxicity through the water. Three ancient sea deities — one for each major island group — have been weakened, their sanctuaries suffocating.

The player, a young diver from the coast, receives a calling. Guided by the deities and armed with gifted relics, they must dive into each region's underwater world, complete the deities' tasks, cleanse the pollution, and restore the sea.

### Deities & Levels

| Level | Region | Deity | Mythos |
|---|---|---|---|
| 1 | Luzon (Mindoro Corridor) | **Amansinaya** (Zone 1), **Haik** (Zone 2), **Neguno** (Zone 3) | Ancient Filipino sea gods of healing, navigation, and the deep |
| 2 | Visayas (Caluya–Ajuy–Padre Burgos) | **Manunubo** (Zone 1), **Kataw Elders** (Zone 2), **Bakunawa** (Zone 3) | Spirit of the ocean, merfolk tribes, the moon-eating sea serpent |
| 3 | Mindanao | TBD | TBD |

### Relics

Each zone's deity gifts the player a relic after completing their tasks. Relics are equippable items with active abilities used in later minigames.

| Relic | Source | Ability |
|---|---|---|
| Radiant Conch | Amansinaya (L1 Z1) | Emits a calming pulse; calls fish herds in Level 2 |
| Compass | Haik (L1 Z2) | Passive: points toward the nearest correct sanctuary in Level 2 |
| Woven Net Fragment | Neguno (L1 Z3) | Throwable net used for selective catching in Level 2 |
| Moontide | Manunubo (L2 Z1) | Reveals and activates invisible tide currents |
| Kataw Gift | Kataw Elder (L2 Z2) | TBD |
| Pearl of Luminescence | Bakunawa (L2 Z3) | TBD (planned: light-based ability) |

---

## 3. Levels at a Glance

### Level 1 — Luzon ✅ Complete
> Scene: `Scenes/Levels/01_Luzon/02_Luzon.unity`

Three zones, each with its own deity, minigames, and relic reward. The diver enters through the Teleport hub and works through each zone from the canyon shallows to the deep tunnel.

| Zone | Deity | Minigames | Relic |
|---|---|---|---|
| 1 – Shallows | Amansinaya | Trash collection (3rd person) → Trash sorting (1st person) | Radiant Conch |
| 2 – Canyon | Haik | Water readings × 3 areas → Coral site prep + planting × 3 areas | Compass |
| 3 – Deep | Neguno | Ghost-net untangling × 3 areas | Woven Net Fragment |

### Level 2 — Visayas 🚧 In Development
> Scene: `Scenes/Levels/02_Visayas/03_Visayas.unity` (being assembled — not yet play-tested end to end)

Three zones across the inner Visayan sea. World models, characters, and all four minigame scripts are in place. The scene now has Bakunawa v2, the Kataw village, tide currents, glow stones, level pieces and the ritual audio placed; the zone 3 quest steps are still being wired.

| Zone | Location | Deity | Minigame | Relic |
|---|---|---|---|---|
| 1 | Caluya | Manunubo | Fish gathering (Radiant Conch + herd ribbon) | Moontide |
| 2 | Ajuy | Kataw Elder | Selective catching (Woven Net) | Kataw Gift |
| 3 | Padre Burgos | Bakunawa | School relocation (Bakunawa chase) → Moon Phase Ritual | Pearl of Luminescence |

### Level 3 — Mindanao 📋 Planned
> Not yet started.

---

## 4. Getting Started

### Prerequisites

- **Unity 6000.x** (the exact version the project was created with)
- **Git LFS** installed — the project uses LFS for FBX models, video files, and audio. Run `git lfs install` once after cloning.
- A Mac with **Xcode** installed (for Metal shader warm-up recordings and iOS builds).

### Cloning

```bash
git clone https://github.com/Ombrei/Lalim_Capstone_Final.git
cd Lalim_Capstone_Final
git lfs pull
```

### Opening

1. Launch **Unity Hub**.
2. Click **Add → Add project from disk** and select the cloned folder.
3. Open the project. Unity will import assets and compile scripts (may take several minutes the first time).
4. ⚠️ If you see a package resolve error about `com.coplaydev.unity-mcp`, **remove that line from `Packages/manifest.json`** — it's a local path to a Claude↔Unity bridge tool that only exists on the original dev machine.

### Playing the Game

**Full flow (recommended):**
1. Open `Scenes/00_BootSequence/00_BootSequence.unity`
2. Press **Play**
3. Order: Boot → Splash → Main Menu → New Game → Loading → Name Input → Tutorial → Teleport Hub → Level 1

**Level 1 directly:**
1. Open `Scenes/Levels/01_Luzon/02_Luzon.unity`
2. Press **Play** — `DevBootstrapper` loads all managers and restores save slot 0

**Replay from the beginning:**
- **Tools ▸ Lalim ▸ Level 1 ▸ Reset Level 1 Progress**

### Dev Hotkeys (Editor & Dev Builds)

| Key | Action |
|---|---|
| P | Skip Name Input and Tutorial |
| F9 | Ability / vitals debug readout |
| F10 | Toggle infinite stamina |
| F11 | Cycle difficulty preset (0 / 0.5 / 1) |
| I | Toggle inventory modal |
| 1 | Press the equipped relic's HUD button |

### Save File Location (macOS)

```
~/Library/Application Support/DefaultCompany/Lalim_Capstone_Final/lalim_save_slot_0.json
```

---

## 5. Project Structure

```
LALIM/
├── Assets/
│   ├── Lalim/                  ← All project-specific content
│   │   ├── Audio/              ← Music, Ambient, SFX (split by level)
│   │   ├── Data/               ← ScriptableObjects: items, difficulty, config
│   │   │   ├── Difficulty/     ← Per-minigame DDA profiles
│   │   │   └── Items/          ← Relic and tool item data
│   │   ├── Materials/          ← URP materials (Luzon/, Visayas/, shared)
│   │   ├── Models/             ← FBX models
│   │   │   ├── Bakunawa/       ← v1 (kept for reference)
│   │   │   ├── Bakunawa_v2/    ← current rigged Bakunawa
│   │   │   ├── Castle/         ← main-menu ruin dressing
│   │   │   ├── Environment/    ← Luzon/, Visayas/
│   │   │   ├── Kataw/          ← 6-body merfolk rig
│   │   │   ├── relics/
│   │   │   └── sea creatures + elements (fbx)/
│   │   ├── Prefabs/
│   │   │   ├── Characters/     ← Kataw NPC prefabs
│   │   │   ├── Core/           ← Per-manager scene prefabs
│   │   │   ├── Creatures/      ← Wanderer / boss creature prefabs
│   │   │   ├── LevelFlow/      ← Reusable quest-system prefabs
│   │   │   ├── LevelTemplate/  ← Zone template prefabs (for Lvl 2 & 3)
│   │   │   ├── Relics/         ← Relic world-model prefabs
│   │   │   └── UI/             ← HUD, modals, loading screen
│   │   ├── Resources/          ← Runtime-loaded assets
│   │   │   ├── Audio/          ← MusicLibrary.asset, SfxLibrary.asset
│   │   │   ├── SeaCreatureClips.asset
│   │   │   └── ShaderWarmup/   ← Per-scene .graphicsstate files
│   │   ├── Scenes/
│   │   │   ├── 00_BootSequence/
│   │   │   ├── 01_SplashScene/
│   │   │   ├── 02_MainMenu/    ← underwater menu scene
│   │   │   ├── 03_NameInput/
│   │   │   ├── 04_Loading/
│   │   │   ├── Levels/
│   │   │   │   ├── 01_Luzon/   ← 02_Luzon.unity  ✅
│   │   │   │   ├── 02_Visayas/ ← 03_Visayas.unity 🚧
│   │   │   │   ├── 03_Mindanao/
│   │   │   │   └── _Template/  ← 00_LevelTemplate.unity (duplicate for L2/3)
│   │   │   ├── Others/         ← TeleportScene/
│   │   │   └── Tutorial/       ← 01_Tutorial.unity
│   │   ├── Scripts/
│   │   │   ├── Abilities/      ← SurgeAbility, RiptideAbility, SwimStaminaPool…
│   │   │   ├── Challenges/     ← Per-minigame components
│   │   │   │   ├── Catching/   ← NetThrower, NetBag, RootPool…
│   │   │   │   ├── Gathering/  ← Herd, ConchCall, Predator…
│   │   │   │   ├── GhostNet/   ← GhostNetMinigame, NetPuzzle…
│   │   │   │   ├── MoonRitual/ ← MoonPhases, MoonCircle
│   │   │   │   └── Relocation/ ← Bakunawa, Sanctuary, ReleaseSpot…
│   │   │   ├── Core/           ← GameConstants, SaveManager, EventBus, MusicDirector…
│   │   │   ├── DDA/            ← ChallengeManager, difficulty knobs
│   │   │   ├── Editor/         ← All Tools ▸ Lalim menu scripts
│   │   │   ├── Editor/Tests/   ← 98 EditMode unit tests
│   │   │   ├── Inventory/      ← InventoryManager, RelicLoadout, ItemData…
│   │   │   ├── Mechanics/      ← PlayerController, creatures, zones, quests
│   │   │   │   └── Quest/      ← LevelFlowDirector, ZoneQuest, QuestStep types
│   │   │   └── UI/             ← HUD, modals, dialogue, toasts
│   │   ├── Sprites/            ← 2D art: characters, logos, UI icons
│   │   ├── UnderwaterMenu/     ← Main-menu scripts, materials, textures, seabed mesh
│   │   └── Videos/             ← LoadingReef_Loop.mp4, PortalSwirl_Loop.mp4
│   ├── URPDefaultResources/    ← URP renderer config
│   └── _Recovery/              ← Unity crash copies (ignored by .gitignore)
├── Packages/                   ← Unity Package Manager
│   └── manifest.json           ← ⚠️ Remove com.coplaydev.unity-mcp line before committing
├── ProjectSettings/
├── CHANGELOG.md                ← Dated change log
├── CONTRIBUTING.md             ← How to contribute
├── DETAILED_UPDATES_2026-10-04.md  ← Deep-dive: Sep 26 → Oct 4 changes
├── DETAILED_UPDATES_2026-10-08.md  ← Deep-dive: Oct 4 → Oct 8 changes
├── DETAILED_UPDATES_2026-10-22.md  ← Deep-dive: Oct 8 → Oct 22 changes
├── GEMINI.md                   ← AI agent rules (Antigravity/Gemini)
├── CLAUDE.md                   ← AI agent rules (Claude)
├── LEVEL1_IMPLEMENTATION_PLAN.md
└── graphify-out/               ← Dev-tool graph dump (ignored by .gitignore)
```

---

## 6. Core Systems

### Scene Flow

```
Boot → Splash → Main Menu ──────────────────────────────────────────
                │                                                    │
              New Game                                          Load Game
                │                                                    │
           Name Input → Tutorial → Loading → Teleport Hub ──────────┘
                                                  │
                                            ┌─────┴──────┐
                                        Luzon (L1)   Visayas (L2)
                                            │             │
                                       Loading ←──────────┘
```

- Loading screens **crossfade** into the incoming scene (never through black).
- The loading screen does real staged loading: memory → scene → shader warm-up.
- `MusicDirector` (`DontDestroyOnLoad`) carries BGM seamlessly across all loads.

### Quest System

Each level follows the same loop per zone:

```
ZoneQuest (toxic water enters) →
  TalkStep (statue dialogue) →
  [Minigame Steps] →
  RelicRewardStep →
  CleanseZoneStep (pollution recedes) →
  UnlockBarrierStep →
  LevelExitStep
```

`LevelFlowDirector` drives zones in order. On resume, it restores completed step IDs and places the player at the last checkpoint.

### Audio Architecture

```
MusicDirector (DontDestroyOnLoad) — session BGM, crossfades in 2.5 s
  └─ MusicLibrary (Resources/Audio/MusicLibrary) — scene → BGM mode
ZoneAmbience (per level) — zone ambient beds, crossfades in 3 s
Sfx + SfxLibrary (Resources/Audio/SfxLibrary) — shared cue set
AmbientOneShots — fire-and-forget positional sounds
UiTapSound (DontDestroyOnLoad) — ButtonTap on every menu press
AudioLevels — Music / SFX / Ambient / Voice mixer knobs
```

To add a new SFX cue: add it to the `SfxCue` enum → add an entry to `SfxLibrary` → call `Sfx.Play(SfxCue.X)`.

### Save System

- **Slot:** `lalim_save_slot_0.json` + `_backup`
- **Data saved:** player name, zone/step progress, checkpoint ID, HP, stamina, cooldowns, equipped relics, swim-bar fill, shuffle draws.
- **Autosave:** triggers after each zone is cleansed, on level exit, and when a relic is equipped.
- **Safety:** never run step completion code against the real save without a backup.

### Dynamic Difficulty Adjustment (DDA)

- Each minigame has a `MinigameDifficultyProfile` asset in `Data/Difficulty/`.
- `ChallengeManager` publishes `ChallengeCompletedEvent` / `ChallengeFailedEvent` and tracks time, errors, and hints used.
- The `ExplorationMetricsTracker` adjusts the next difficulty knob value (0 = easiest, 1 = hardest).
- Knob names are defined in `Level1DifficultyKnobs` and `Level2DifficultyKnobs`.

### Abilities

| Ability | Input | Key Rule |
|---|---|---|
| Surge (basic) | Tap Shift / button | 8 m dash, chainable 3× as a combo; charges the swim bar |
| Riptide (basic) | Hold 0.6 s | 1.75× speed; spends swim-bar segments (10 s each) |
| Spirit Sense | Q | Costs 1 orb; wisp leads to objective; Spirit Glow on targets |
| Tidebond | — | Ride the dugong at 2.25×; 7 s cooldown (was 30 s) |
| Seagrass Mend | Dugong card | Heals 30 HP over 3 s; 40 s cooldown |
| Bubble Barrier | — | 20 s toxic + hit shield; charges from swimming and skills |

**Swim stamina bar (new as of Oct 8):** 6 segments. Surge combo 1 = +⅓ seg, combo 2 = +½ seg, combo 3 = +1 full seg. Riptide spends 1 segment per 10 s.

---

## 7. Gameplay Mechanics

### Minigames — Level 1

| Minigame | View | Mechanic |
|---|---|---|
| Trash Collection | 3rd person | Swim to glowing trash, tap to pick up |
| Trash Sorting | 1st person | Drag trash into the correct shrine bowl |
| Water Readings | 1st person | Tap STOP while the needle is in the green band |
| Coral Site Prep & Planting | Top-down | Swipe algae → tap dead corals → plant Coral Seed |
| Ghost-Net Untangling | Top-down | Tap tangled rope nodes to untangle the net mesh |

### Minigames — Level 2 (Scripts complete; scene being wired)

| Minigame | Mechanic |
|---|---|
| Fish Gathering (Caluya) | Blow the Radiant Conch to calm scared herds and guide them to Manunubo's light; evade predator sharks using hiding zones |
| Selective Catching (Ajuy) | Throw the Woven Net at the right fish schools (Spirit Sense colours targets); don't catch protected species |
| School Relocation (Padre Burgos) | Carry schools in the net-bag to matching sanctuaries while Bakunawa patrols and hunts you |
| Moon Phase Ritual (Padre Burgos) | Stand in 8 shuffled stone circles in moon-phase order (New → Waning Crescent) to appease Bakunawa; a wrong circle restarts the ritual |

### Underwater Feel

- Behind-the-back third-person camera with spring arm.
- `DiverIdleFloat`: bob + sway + 72° upright lean when still.
- `TideCurrent`: path-based currents, invisible until Moontide relic is equipped.
- Rigged sea creatures animated via `CreatureRig` + `SeaCreatureClips`; baked-mesh Visayas creatures driven by `AmbientSwimmers`.
- `KatawVillager`: Kataw NPCs bob, watch the diver, glow amber (quest givers), and emit ambient hums.

---

## 8. Editor Tools

All tools live under **Tools ▸ Lalim** in the Unity menu bar.

### Level 1 Tools

| Tool | What it does |
|---|---|
| **Level 1 ▸ Build Level Flow** | Idempotent build of all zone/step objects in 02_Luzon |
| **Level 1 ▸ Reset Level 1 Progress** | Clears save slot 0 progress for Level 1 |
| **Level 1 ▸ Scatter Zone 1 Trash** | Re-places zone 1 trash pickups flat on the sand |
| **Level 1 ▸ Set Up Statue Talk Framing** | Places TalkPoint/TalkDugong + 3/4 camera for each statue |
| **Level 1 ▸ Drape Ghost Nets** | Lowers and levels ghost nets over their animals |
| **Level 1 ▸ Find Zone 2 Sites** | Geometry-tests zone 2 floor, installs pool into `ZoneSiteShuffle` |

### Level 2 Tools

| Tool | What it does |
|---|---|
| **Level 2 ▸ Build Zone 3 Glow Stones** | Fluorescent pebble clusters along Trench B and the Padre Burgos abyss |
| **Play From ▸ Level 1 · Zone 1–3 / Level 2 · Zone 1–3** | Launches directly into a zone with a synthetic save; the real save is backed up and restored on Play stop |
| **Play From ▸ Restore My Save** | Restores the real save if Unity crashed during a Play From test |
| **Relics ▸ Build Pearl of Luminescence** | Optimised Pearl mesh + material; rebuilds `PF_Relic_PearlOfLuminescence` in place |
| **Relics ▸ Build Kataw Gift Placeholder** | Rebuilds `PF_Relic_KatawGift_PH` with the necklace placeholder |

**Code-only Level 2 builders (no menu item yet)** — call from an editor script or the Unity MCP bridge with `03_Visayas` open:

| Class | What it does |
|---|---|
| `Level2Currents.Build(…)` | Threads Moontide current routes through Visayas open water |
| `Level2KatawVillage.Build(seed)` | Places Kataw family groups in Ajuy's root village |
| `Level2LevelPieces.Build…()` | Ajuy throwing ledges and lip lantern; Padre Burgos abyss ladders and hide caves |

### Creature Tools

| Tool | What it does |
|---|---|
| **Use Rigged Sea Creatures** | Builds `SeaCreatureClips.asset`; swaps Luzon's static meshes for rigged FBX; updates wanderer prefabs |

### Scene Tools

| Tool | What it does |
|---|---|
| **Add Solid Scenery Colliders** | Adds mesh colliders to arches, walls, rocks by name prefix |

### Level Template Tools (for Levels 2 & 3)

| Tool | What it does |
|---|---|
| **Level Template ▸ Build Template Prefabs** | Saves per-manager `Core/` prefabs + zone template prefabs |
| **Level Template ▸ Create Template Scene** | Creates `Scenes/Levels/_Template/00_LevelTemplate.unity` |
| **Level Template ▸ Add Missing Level Pieces** | Surgically adds template pieces to an existing scene |
| **Level Template ▸ Fix Up Level IDs** | Stamps zone/step/checkpoint IDs from the scene's region. **Required after duplicating the template.** |

### Build & Setup Tools

| Tool | What it does |
|---|---|
| **Abilities ▸ Build Ability HUD** | Rebuilds `GameHUD.prefab` skill buttons |
| **Build Reusable Level Prefabs** | Saves all `Prefabs/LevelFlow/` prefabs from the open scene |
| **UI ▸ Use UI Prefabs In Open Scene** | Applies shared UI prefabs to the open scene |

---

## 9. Tests

**98 EditMode tests**, all passing as of 2026-10-04.

```
Window ▸ General ▸ Test Runner ▸ EditMode
Filter assembly/group to: Lalim.Tests
```

> ⚠️ **Do NOT run unfiltered.** An unfiltered run pulls in ~570 URP package tests, and one (`EditorTests.ValidateDefaultRendererAfterReset`) hangs indefinitely.

Tests cover: abilities, stamina math, dialogue, DDA, save/load, inventory, quest steps, and scene-loading helpers.

**New pure-math classes that need tests (not yet written):**
- `SwimStaminaPool` — charge rules, Riptide segment consumption, refill
- `RelicLoadout` — equip/unequip, cooldown timing
- `ZoneSiteShuffle` — seed-stable draw, spacing
- `MoonPhases` — glyph generation, phase colour lookup, order correctness
- `WaterBrightness.Contrast` — luminance formula at the 0.25 and 0.45 boundaries
- `PlanktonByZone` — band selection and blend rate
- `SchoolReleaseSwirl` — figure-eight path math, done callback timing

---

## 10. Documentation & Change Log

| Document | What it covers |
|---|---|
| **[DETAILED_UPDATES_2026-10-22.md](DETAILED_UPDATES_2026-10-22.md)** | All work from 2026-10-08 → 2026-10-22. Covers underwater main menu, Bakunawa v2 model & boss encounter, Pearl of Luminescence & Kataw Gift 3D models/materials, dialogue portraits, Moon Phase Ritual minigame, School Relocation, Selective Catching, Fish Gathering, and Level 2 editor tools. ~63 files/folders added; committed as `372643b` + `372d601`. |
| **[DETAILED_UPDATES_2026-10-08.md](DETAILED_UPDATES_2026-10-08.md)** | All work from 2026-10-04 → 2026-10-08. Covers audio system, swim-bar redesign, inventory & relic HUD, Level 2 Visayas groundwork (world models, Bakunawa, Kataw, 3 minigames), creature animation pipeline, and scene tools. ~86 files added. |
| **[DETAILED_UPDATES_2026-10-04.md](DETAILED_UPDATES_2026-10-04.md)** | All work from 2026-09-26 → 2026-10-04. Covers the full skill kit, HUD, Level 1 build (3 zones, 5 minigames), scene flow, dialogue, relics, teleport hub, loading screen, and more. 545 files added. |
| **[CHANGELOG.md](CHANGELOG.md)** | Concise version-keyed change log. |
| **[CONTRIBUTING.md](CONTRIBUTING.md)** | How to set up, branch, submit work, and follow code conventions. |
| **[LEVEL1_IMPLEMENTATION_PLAN.md](LEVEL1_IMPLEMENTATION_PLAN.md)** | Original implementation plan for Level 1 (zones, lore research, DDA, achievements). |
| **[CHALLENGES_SETUP_GUIDE.md](CHALLENGES_SETUP_GUIDE.md)** | How to set up and configure each type of minigame challenge. |
| **[GEMINI.md](GEMINI.md)** | Rules and conventions for AI assistants using the Antigravity/Gemini toolchain. |
| **[CLAUDE.md](CLAUDE.md)** | Rules and conventions for AI assistants using Claude. |

---

## 11. Known Issues

- **`03_Visayas.unity` not yet play-tested end to end** — world models, minigames, NPCs, ritual audio and builder output are placed; the zone 3 quest steps are still being wired.
- **No Padre Burgos zone BGM** — `Audio/Music/PadreBurgos/` holds only the 8 ritual melodies and the Bakunawa lullaby.
- **Level 2 dialogue is draft placeholder** — needs a final writer's pass for character voice and tone.
- **Kataw Gift & Pearl abilities** — 3D models, icons, and item assets are complete; active abilities are TBD by design.
- **Shader warm-up recordings only exist for Metal (Mac/iOS)** — Android (Vulkan/GLES) needs recordings from a connected device.
- **Old tracked dev files** — `.gitignore` now covers `graphify-out/`, `Assets/Screenshots/`, `.obsidian/` and `Captures/`, but `Assets/Lalim/Scripts/Core/graphify-out/`, one screenshot and `.claude/settings.json` were committed earlier and are still tracked. Untrack them with `git rm -r --cached <path>` if they shouldn't be in the repo.
- **`Packages/manifest.json`** has a local-path MCP package — remove before committing to shared branches.

---

## 12. Contributing

See **[CONTRIBUTING.md](CONTRIBUTING.md)** for the full guide.

Quick summary:
- Branch from `hym-test` (the active dev branch)
- Follow the conventions in `GEMINI.md` (AI agents) or this README (humans)
- Run the `Lalim.Tests` EditMode suite before submitting
- Never commit `Assets/_Recovery/`, `graphify-out/` (repo root) or `Assets/Lalim/Scripts/Core/graphify-out/`, `Assets/Screenshots/`, `Captures/`, `.claude/`, or the local MCP package path (`.gitignore` covers all of these except `.claude/` and the manifest line)

---

## 13. License

See **[LICENSE](LICENSE)** for terms.

This project is an academic capstone and is not currently open for commercial use.

---

*Last updated: 2026-10-22 — if this README disagrees with the code or detailed update docs, the code wins.*
