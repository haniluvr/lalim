# Contributing to Lalim

Thank you for working on Lalim! This guide covers everything you need to know — whether you are a teammate, a new developer onboarding, or an AI assistant helping with the codebase.

> For AI-specific rules, also read **[GEMINI.md](GEMINI.md)** (Antigravity/Gemini agents) or **[CLAUDE.md](CLAUDE.md)** (Claude agents).

---

## Table of Contents

1. [Before You Start](#1-before-you-start)
2. [Branching Strategy](#2-branching-strategy)
3. [Setting Up Your Environment](#3-setting-up-your-environment)
4. [Code Style & Conventions](#4-code-style--conventions)
5. [Unity-Specific Rules](#5-unity-specific-rules)
6. [Audio Rules](#6-audio-rules)
7. [UI Rules](#7-ui-rules)
8. [Minigame Rules](#8-minigame-rules)
9. [Save System Rules](#9-save-system-rules)
10. [Testing](#10-testing)
11. [Submitting Changes](#11-submitting-changes)
12. [What NOT to Commit](#12-what-not-to-commit)
13. [Asking for Help](#13-asking-for-help)

---

## 1. Before You Start

Read the following documents **before touching any code**:

| Priority | Document | Why |
|---|---|---|
| 🔴 Required | **[DETAILED_UPDATES_2026-10-22.md](DETAILED_UPDATES_2026-10-22.md)** | Most recent state of the codebase (underwater menu, Bakunawa, Level 2 minigames, Moon Phase Ritual) |
| 🔴 Required | **[DETAILED_UPDATES_2026-10-08.md](DETAILED_UPDATES_2026-10-08.md)** | Audio system, swim bar, inventory & relics, Level 2 groundwork |
| 🔴 Required | **[DETAILED_UPDATES_2026-10-04.md](DETAILED_UPDATES_2026-10-04.md)** | How Level 1 was built; conventions established |
| 🟡 Important | **[README.md](README.md)** | Project overview and system map |
| 🟡 Important | **[CHANGELOG.md](CHANGELOG.md)** | Version-by-version summary of changes |
| 🟡 Important | **[GEMINI.md](GEMINI.md)** or **[CLAUDE.md](CLAUDE.md)** | Agent-specific rules |
| 🟢 Reference | **[CHALLENGES_SETUP_GUIDE.md](CHALLENGES_SETUP_GUIDE.md)** | How to set up new minigames |

---

## 2. Branching Strategy

| Branch | Purpose |
|---|---|
| `main` | Stable, reviewable baseline. Protected. |
| `hym-test` | **Active development branch.** This is where all current work happens. |
| `prototype-test` | Old prototype; do not merge back. |
| Feature branches | Branch from `hym-test` for large isolated features (e.g. `feature/visayas-scene-assembly`). |

**Create a feature branch:**
```bash
git checkout hym-test
git pull
git checkout -b feature/your-feature-name
```

**Merge back:**
```bash
git checkout hym-test
git merge --no-ff feature/your-feature-name
git push
```

---

## 3. Setting Up Your Environment

### Required Tools

- **Unity 6000.x** (match the project version exactly)
- **Git LFS** — `git lfs install` once after cloning
- **Xcode** (macOS) — for Metal shader warm-up recordings and iOS builds
- **.NET SDK** compatible with Unity 6 (for IDE tooling)

### First-Time Setup

```bash
# 1. Clone
git clone https://github.com/Ombrei/Lalim_Capstone_Final.git
cd Lalim_Capstone_Final
git lfs pull

# 2. Remove the local MCP package path from Packages/manifest.json
#    (the com.coplaydev.unity-mcp line — it's a local path on one specific Mac)

# 3. Open in Unity Hub → Add project from disk
```

### Package Issues

If Unity complains about packages on first open:
- `com.coplaydev.unity-mcp`: **remove from `manifest.json`** — this is a local-only Claude bridge tool.
- `com.unity.cloud.gltfast` and `com.unity.visualeffectgraph`: required, should download automatically.

### Recommended IDE

- **JetBrains Rider** (best Unity integration) or **Visual Studio Code** with the C# Dev Kit extension.

---

## 4. Code Style & Conventions

### Naming

| Thing | Convention | Example |
|---|---|---|
| Classes | PascalCase | `MusicDirector`, `SwimStaminaPool` |
| Methods | PascalCase | `ChargeForSurge()`, `Play()` |
| Private fields | `_camelCase` | `_chargesUsed`, `_comboTimer` |
| Public properties | PascalCase | `Swim`, `RiptideSecondsPerSegment` |
| Constants | `UPPER_SNAKE_CASE` | `RADIANT_CONCH`, `SWIM_STAMINA_SEGMENTS` |
| Coroutines | `*Routine` | `FadeInRoutine()` |
| Events | Past tense in the event name | `RelicEquipChangedEvent`, `ChallengeCompletedEvent` |

### Files

- One class per file (except tiny helper structs and enums).
- File name matches the primary class name exactly.
- `internal class CommandScript` for Unity MCP execute-code scripts.

### Comments & Docs

- All public classes and public methods should have XML `<summary>` docs.
- Keep doc comments and existing comments when modifying a file — don't delete comments that aren't yours.
- Use `// TODO:` (not `//TODO`) for work-in-progress notes.

### General

- Prefer composition over inheritance for MonoBehaviours.
- Keep MonoBehaviours thin — move pure logic into plain C# classes (like `SwimStaminaPool`).
- Don't use `FindObjectOfType` at runtime; inject dependencies in `Awake` / `Start` or use `EventBus`.
- Avoid `Update()` polling; prefer events (`EventBus.Subscribe`) where possible.
- Use `GameConstants` for all magic numbers and string IDs (relic IDs, save keys, etc.).

---

## 5. Unity-Specific Rules

### Scenes

1. **Never stop an active Play session** on the main dev machine — scripts recompile during play and domain reload can corrupt state. Stop Play before saving `.cs` files.
2. **Scene flow:** a loading screen never fades to black at the end. Leaving game scenes always crossfades into the loading screen. Name Input → Tutorial has no loading screen. Never bypass `SceneLoader.LoadScene(…)`.
3. **Camera:** keep the **behind-the-back** style. Don't switch to free orbit without asking.
4. **Notifications / toasts:** no banners or toasts for things the player can already see on screen. Rewards go in the one Quest Complete toast after dialogue. Order is always: ACT → Quest Complete → Achievement. `ZoneBannerUI` always yields to `ActToastUI`.
5. **New level scenes:** duplicate `Scenes/Levels/_Template/00_LevelTemplate.unity`, then run **Tools ▸ Lalim ▸ Level Template ▸ Fix Up Level IDs** before any play-test. Zone IDs from the template start with `tmpl_` and will clash with other levels' save data.
6. **Zone play-tests:** use **Tools ▸ Lalim ▸ Play From ▸ Level N · Zone M**. It backs up the real save and restores it when Play stops. After a crash, run **Play From ▸ Restore My Save**.
7. **Level 2 builders rebuild from scratch.** `Level2Currents`, `Level2KatawVillage`, `Level2LevelPieces` and `Zone3GlowStones` delete their own earlier output before rebuilding, so hand edits to that output are lost. The first three have no menu item yet; call their static `Build…()` methods from an editor script or the Unity MCP bridge (see DETAILED_UPDATES_2026-10-22 §6).

### Prefabs

- **Edit the prefab, not the scene instance** — shared UI and systems live in `Prefabs/LevelFlow/` and `Prefabs/UI/`. Editing an instance in one scene won't propagate.
- The `GameHUD.prefab` and `LoadingScreen.prefab` layouts were hand-adjusted by the owner. Use **Tools ▸ Lalim ▸ UI ▸ Use UI Prefabs In Open Scene** to apply them; don't rebuild them unless you know what you're doing.
- When adding new manager-level objects to a scene, save them as prefabs in `Prefabs/Core/` using **Level Template ▸ Build Template Prefabs** (run with `02_Luzon` open).
- **Relic builders overwrite prefabs in place:** **Relics ▸ Build Pearl of Luminescence** rebuilds `PF_Relic_PearlOfLuminescence`, and **Relics ▸ Build Kataw Gift Placeholder** rebuilds `PF_Relic_KatawGift_PH`. Back up manual edits first.

### URP Gotchas

- **Render order:** set draw order with `_QueueOffset` and `_Blend` (2 = additive); don't rely on `renderQueue` alone.
- **Emission:** the Lit material's `_EMISSION` keyword can be stripped on save. Use **Unlit** for things that must glow (e.g. net knots).
- **Sprites/Default:** don't use it for 3D objects. Use URP Unlit (alpha clip) or `URP/Particles/Unlit`.
- **Shader variants in builds:** always back runtime-created materials with material assets so their shader variants are included in builds.
- **Water brightness:** VFX that must stay readable as the water clears read `WaterBrightness.Contrast` (0 = dark, 1 = bright), not `RenderSettings.fogColor` directly.

### Models & Creatures

- The sea-creature FBX models face **−Z**; rotate them 180° in prefabs.
- For `WanderingMarineAnimal`, use `headAxis` (the positive end of the longest axis = head).
- To swap Luzon's static creature meshes for rigged versions, run **Tools ▸ Lalim ▸ Use Rigged Sea Creatures** in `02_Luzon`. Don't edit `SeaCreatureClips.asset` by hand.
- **Bakunawa:** use `PF_Bakunawa_v2_Model.prefab` (`Models/Bakunawa_v2/`) for new scene work. Keep the v1 `PF_Bakunawa_Model.prefab` and `Bakunawa.fbx` for reference; don't delete them.

### TextMeshPro

- Use the **"Padded" PlusJakartaSans SDF** assets for runtime UI text (the non-padded variants show dark boxes behind glyphs at runtime).

---

## 6. Audio Rules

1. **Shared SFX:** always use `Sfx.Play(SfxCue.X)` or `Sfx.PlayAt(SfxCue.X, position)` for cues in `SfxLibrary`. Never `AudioSource.PlayClipAtPoint` for these — they won't respect volume settings.
2. **One-off sounds:** use `AmbientOneShots.PlayAt(clip, pos)` only for unique positional sounds not worth enumerating in `SfxCue`.
3. **Music:** never play BGM via a raw `AudioSource.Play`. Use `MusicDirector` (through `MusicLibrary` scene entries) so crossfades and level takeover work.
4. **`MusicLibrary` and `SfxLibrary`** are partly filled. A cue or scene with no clip assigned is simply silent, so check the asset before assuming a sound is broken. Padre Burgos has no zone BGM yet.
5. **No 3D audio on UI elements.** All UI sounds (button taps, toast chimes) go through the SFX mixer channel, not the spatial audio system.
6. `UiTapSound` handles `ButtonTap` automatically on any `Button` or `Toggle`. Only opt out by placing a `NoTapSound` marker (`Scripts/UI/NoTapSound.cs`) on the parent. Don't add per-button opt-out flags.
7. **Bakunawa and ritual audio live on their components**, not in `SfxLibrary`: Bakunawa's roar, hiss, strike, swim, growl, calm and chase clips are fields on `Bakunawa`; the 8 phase melodies and the lullaby are fields on `MoonPhaseRitualStep`. Assign them there.

---

## 7. UI Rules

1. **Language:** player-facing text is short and plain. Glass cards show one hint line at a time.
2. **Pause clock:** HUD animations use `PauseClock.DeltaTime` (not `Time.unscaledDeltaTime`) so a game-pause freezes them.
3. **Inventory / modals:** the world pauses (`timeScale = 0`) while the inventory modal is open. Don't open other world-interacting UI simultaneously.
4. **Right-handed mode:** the HUD mirrors horizontally in right-handed mode (`HudHandedness`). Any new HUD elements must respond to this setting.
5. **`CodexModalUI` and `MapModalUI` are deleted** — remove any lingering references before committing.
6. **`TransitionScreen`:** don't bypass it with `ScreenFader` for game-to-game scene transitions.

---

## 8. Minigame Rules

1. **New minigame steps** extend `QuestStep`. Minigames played at several stations (the Level 1 pattern) also implement `IAreaMinigame` and sit on an `AreaStation` under an `AreaMinigameStep`; whole-zone minigames (all Level 2 steps) extend `QuestStep` directly.
2. **DDA:** every minigame needs a `MinigameDifficultyProfile` asset in `Data/Difficulty/`. Define knob names in `LevelXDifficultyKnobs`.
3. **SFX cues:** call `Sfx.Play(SfxCue.Correct)` / `Sfx.Play(SfxCue.Wrong)` etc. at the right moments. The `ChallengeManager` events (Completed/Failed) trigger sound automatically, but per-action feedback does not.
4. **HP floor:** during first-person minigames, toxic drain continues but HP cannot drop below 1. `ToxicWaterTracker` handles this.
5. **Relic actions:** check `RelicLoadout.IsEquipped(id)` and react to `RelicEquipChangedEvent` and `RelicUsedEvent`. Don't control `RelicHudButtons` directly from a minigame.
6. **Level 2 dialogue** in `Level2DialogueScripts` is a draft that still needs a writer's pass. Don't design or test UI based on the specific wording — it will change.
7. **NPC entrances:** any NPC with a pre-conversation animation implements `ITalkPrelude` (`PlayBeforeTalk`, `Prelude`, `TryGetTalkFocus`). Don't hand-wire special cases into `StatueDialogueRouter`.
8. **Moon Phase Ritual:** the circles are shuffled each session. Don't hard-code circle positions or the expected phase layout; only the stepping order (New Moon → Waning Crescent) is fixed.

---

## 9. Save System Rules

1. **Always back up** `lalim_save_slot_0.json` and its `_backup` before a Play session that might trigger saves.
2. **`SaveManager.Origin = Unknown`** only blocks `AutoSave()`. `SaveGame()` and step-completion still write — never test step completion code against the real save without a backup.
3. **New save fields:** add them to `SaveData`, give them a sensible default, and handle the case where the field is missing (loaded from an older save).
4. **Zone / step / checkpoint IDs** are the save keys. They must be unique across the entire game. Use the IDs in `GameConstants.Zones` (Visayas: `VISAYAS_CALUYA` / `VISAYAS_AJUY` / `VISAYAS_PADRE_BURGOS` = `visayas_zone_a/b/c`). Run **Fix Up Level IDs** after duplicating the template.
5. **`equippedRelicIds`** is now part of `SaveData`. Use `RelicLoadout.Equip()`/`Unequip()` — don't write to `SaveData.equippedRelicIds` directly.
6. **Level 2 play-tests go through Play From** (see §5). Never hand-edit the save to skip to a Level 2 zone.

---

## 10. Testing

```
Window ▸ General ▸ Test Runner ▸ EditMode
Filter to assembly/group: Lalim.Tests
```

**Rules:**
- Run the full `Lalim.Tests` suite before submitting any PR.
- **Do not run unfiltered** — it pulls in URP package tests and one hangs.
- Play-mode testing writes to the real save. Copy and restore `lalim_save_slot_0.json` before any play-mode test that triggers saves.
- Never run a step's completion code (zone cleanse, level exit, relic grant) against the real save without a backup.

**New testable pure-math classes (no Unity lifecycle needed):**
```csharp
// SwimStaminaPool — create directly, no mocking needed
var pool = new SwimStaminaPool(6);
pool.ChargeForSurge(comboIndex);
pool.StartRiptide();
pool.DrainRiptide(dt);

// RelicLoadout — set up SaveData + mock SaveManager first
// ZoneSiteShuffle — test seed-stable draw and spacing enforcement
// MoonPhases — glyph generation, phase colour lookup, order correctness
// WaterBrightness.Contrast — luminance formula at the 0.25 and 0.45 boundaries
// PlanktonByZone — band selection and blend rate
// SchoolReleaseSwirl — figure-eight path math, done callback timing
```

None of these have tests yet; the suite is still the 98 tests from 2026-10-04.

---

## 11. Submitting Changes

### Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>: <short description (max 72 chars)>
```

Types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `style`

**Good examples:**
```
feat: add Visayas environment, audio, and core mechanics
fix: prevent swim bar from over-charging on rapid combo
docs: update CONTRIBUTING with audio rules
refactor: extract SwimStaminaPool from SurgeAbility
test: add unit tests for RelicLoadout cooldown timing
```

**Bad examples:**
```
update stuff
fixed bug
WIP
feat: add Visayas environment assets, audio, and core game mechanics scripts  ← too long
```

### Before Pushing

- [ ] `Lalim.Tests` EditMode suite passes
- [ ] No missing-script warnings in the Console
- [ ] `Packages/manifest.json` does not contain the local `com.coplaydev.unity-mcp` path
- [ ] `Assets/_Recovery/`, `Assets/Screenshots/`, `Captures/` and `.obsidian/` are not staged
- [ ] No `graphify-out/` folder is staged (repo root or `Assets/Lalim/Scripts/Core/`)
- [ ] `.claude/` is not staged (unless intentional)
- [ ] Large binary files go through Git LFS (`.gitattributes` already routes FBX, MP3, WAV and other binaries; check anything new)

### Pull Requests

1. Target branch: `hym-test`
2. Title: use the same Conventional Commit format as the commit message
3. Description: link to the relevant `DETAILED_UPDATES_*.md` section or summarize what changed and why
4. Request review from the project owner before merging

---

## 12. What NOT to Commit

| Path / File | Why |
|---|---|
| `Assets/_Recovery/*.unity` | Unity crash-recovery copies |
| `graphify-out/` (repo root) and `Assets/Lalim/Scripts/Core/graphify-out/` | Dev-tool dependency graph dumps |
| `Assets/Screenshots/` | In-editor screenshots |
| `Captures/` | Screen captures |
| `Assets/_AxisTest/` | Test scene for axis orientation |
| `.obsidian/` | Local Obsidian vault settings |
| `.claude/` | Local Claude agent config |
| `Library/`, `Logs/`, `Temp/`, `UserSettings/` | Unity-generated |
| `Packages/manifest.json` with the local MCP path | Breaks package resolution for everyone else |
| `mono_crash.mem.*.blob` | Unity crash dump files |

`.gitignore` covers the Unity folders, `mono_crash.*`, `graphify-out/`, `Captures/`, `Assets/Screenshots/`, `Assets/_Recovery/`, `Assets/_AxisTest/` and `.obsidian/`. It does **not** cover `.claude/` or the manifest MCP line, so check those by hand.

⚠️ Ignoring a path doesn't untrack files that were committed before the rule existed. `Assets/Lalim/Scripts/Core/graphify-out/` (47 files + `.meta`), one screenshot in `Assets/Screenshots/` and `.claude/settings.json` are still tracked. Remove them from the index with `git rm -r --cached <path>` if they shouldn't be in the repo.

---

## 13. Asking for Help

- **Something doesn't match this doc?** The code wins — update the doc.
- **Can't find where a system lives?** Check the relevant `DETAILED_UPDATES_*.md` file — they have section-by-section file inventories.
- **AI agents:** read `GEMINI.md` (Antigravity) or `CLAUDE.md` (Claude) for agent-specific rules; use no more than 3–4 file operations to orient yourself before asking the owner for more context.
- **Questions about design decisions:** check `LEVEL1_IMPLEMENTATION_PLAN.md` first; the decisions made there (dialogue style, toast order, camera style, DDA approach) still apply to Levels 2 and 3.

---

*Last updated: 2026-10-22*
