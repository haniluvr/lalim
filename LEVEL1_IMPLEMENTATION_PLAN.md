# Level 1 (Luzon) — Implementation Plan

**Target scene:** `Assets/Lalim/Scenes/Levels/01_Luzon/02_Luzon.unity` (new and final)

> **Renamed 2026-10-02:** this scene was built as `02_LuzonNew` and is now `02_Luzon`. The old `02_Luzon` became the tutorial, `Assets/Lalim/Scenes/Tutorial/01_Tutorial.unity`, with its lighting folder and `01_TutorialSettings.lighting`. The legacy tutorial scene was deleted. Saves that remember `02_LuzonNew` are migrated on load (`SaveData.MigrateSceneNames`). Mentions of `02_LuzonNew` below refer to this scene.
**Exit scene:** `Assets/Lalim/Scenes/Others/TeleportScene/TeleportScene.unity`
**Status:** Level 1 is implemented end to end (P0–P7). See **Implementation progress** below.

### Decisions confirmed (2026-10-02)

| # | Decision |
|---|---|
| 1 | **Trash collection starts the moment Amansinaya's dialogue ends**, with no interact button. It ends when **no trash is left in the scene**. |
| 2 | **No return-to-statue step.** Collecting the last piece of trash moves straight into minigame 2 (trash sorting). |
| 3 | **HP drain pauses** during dialogue and during 1st-person minigames. |
| 4 | ~~The water readout asks which reading is abnormal.~~ **Changed (2026-10-02):** no diagnosis step. Each reading locks and shows **"Normal"**. |
| 5 | **Coral Seed is a new item.** The Coral Replanting Kit is **deleted** (§5.4 lists every reference to clean up). |
| 6 | **Toxic zones drain 0.5 HP per second in Level 1.** Drain scales with difficulty, but **only between levels**: it is set once at the start of each level from the player's performance so far, and never changes mid-level (§2.4). |
| 7 | **Zone 3 has no item reward.** The tunnel is the reward. |
| 8 | **Ghost nets free 3–7 animals per area, set by DDA.** Species are small-to-mid sized, randomized, and rarely repeat (§5.6). |
| 9 | **All four proposed achievements are in:** Clean Sweep, Steady Hands, Net Breaker, Untouched (§2.5). |
| 10 | **The Dialogue HUD uses placeholder icons for now.** |
| 11 | **Spirit Sense is part of Level 1 navigation**: glow, wisp and hidden trash (§2.6.3). |
| 12 | **Audio navigation:** BGM gets louder near the next target and quieter far from it, on top of the existing compass and escalating hints (§2.6.2, §2.6.4). |
| 13 | **Checkpoints let a player who leaves the game resume where they were** (§2.7). |
| 14 | **New games auto-save** to the preferred slot from the first Level 1 checkpoint (§2.7). |
| 15 | **The whole level starts polluted and toxic, and it looks it.** Each cleared zone makes the **whole** environment gradually cleaner, so the more zones are cleared, the cleaner Level 1 looks (§2.8). |
| 16 | **Statue dialogue is grounded in researched lore** for Amansinaya, Haik and Neguno (§3.4). |

### Implementation progress (2026-10-02)

| Phase | State | Notes |
|---|---|---|
| P0 scene prep | ✅ Done | `02_LuzonNew` replaces `02_Luzon` in Build Settings and `GameConstants.Scenes.LUZON`. The scene has its own `[DDAManager]`. Checkpoints are placed. `Zone3_Barrier`, `Tunnel_Barrier` and the level exit are added. |
| P1 framework | ✅ Done | `LevelFlowDirector`, `ZoneQuest`, step types, `ObjectiveTracker`, `ProximityMusic`, `PollutionDirector`, toast queue, `ZoneBannerUI`, HUD `ActionButton` (chat/interact icons), level toxicity (0.5 HP/s, paused in dialogue and 1P), save and resume (checkpoint, app pause, new-game auto-bind). Statues have the §3.4 scripts. |
| Zone 1 minigames | ✅ Playable | Trash collection (3P, DDA count, every trash prop cleared) → automatic sorting at the altar (existing `SegregationAltar`). |
| P2 dialogue HUD | ✅ Done | New NPC/Player boxes (name top-left for NPCs, top-right for the player; text from the left). Top-left buttons: Review (full-screen dark log; NPC lines left, player right; scene paused), Auto, Speed (1 / 1.2 / 1.5 / 2 shown in the circle), Pause. Auto and speed are remembered. The quest box and tool panel hide during dialogue. |
| P3 DDA extensions | ✅ Done | Pickup rate (time to first correct action), hint reliance (only for minigames with hints), and a history blend over all earlier minigames (saved). `MinigameDifficultyProfile` assets in `Data/Difficulty` (Easy/Normal/Hard knobs) drive trash count and hidden trash, sorting bowl labels, calibration, coral prep, and the ghost-net values for later. |
| P4 zone 2 minigames | ✅ Done | Water calibration (3 stabilise-the-needle meters, then "which reading is out of range?") and coral site prep (swipe algae, tap skeletons, avoid living decoys, plant the seed). The Coral Replanting Kit is deleted. |
| P5 ghost nets | ✅ Done | 3D net with draggable knots, red ropes (bright red where they cross), 3–7 trapped animals from the level-wide shuffle bag, the Freed Ocean Friends tray, codex unlocks, hints (snap the worst knot to its solved spot), and a swipe up to haul the net away. The scene's net props disappear afterwards. |
| P6 exit | ✅ Done | Foggy-water barriers for zone 3 and the tunnel, with "Complete Zone N to pass through." The well floor opens when the tunnel unlocks, and the exit is at the bottom of the well (y ≈ −106). The teleporter "unlocked" reveal is still to do. |
| P7 polish | ✅ Done | Music: Fairy Water Cave (streamed). Achievements: Clean Sweep, Steady Hands, Net Breaker, Untouched (toast plus save). Teleport scene: one-time "Visayas teleport unlocked" banner and burst. Dialogue HUD revisions: the review overlay covers the buttons, Auto shows a spinner, Pause toggles to Play, the X has no background, and the title is "Story Review". Tools ▸ Lalim ▸ Level 1 ▸ Reset Level 1 Progress for replaying from the start. |

**Reusable prefabs for Levels 2 and 3** (Tools ▸ Lalim ▸ Build Reusable Level Prefabs → `Assets/Lalim/Prefabs/LevelFlow/`):

| Prefab | What it is |
|---|---|
| `PF_LevelSystems` | Level director, objective tracker, proximity music, pollution director, zone banners, achievements. One per level scene. |
| `PF_Zone` | Zone quest with its toxic water (level-rate drain, doesn't slow stamina) and murk particles. Add quest steps as children. |
| `PF_FoggyBarrier` | Foggy-water barrier with its "Complete … to pass through." message. |
| `PF_Station_WaterCalibration` / `_CoralSitePrep` / `_GhostNet` / `_Placeholder` | Area stations (interact icon, view point, marker) with their minigame. |
| `PF_LevelExit` | End-of-level trigger used by `LevelExitStep`. |
| `PF_DialogueSystem` | Dialogue box (NPC/Player), Story Review, dialogue buttons, DialogueManager. |
| `PF_ToastSystem` / `PF_QuestObjectiveUI` | Achievement-title toast and the quest objective box. |

**Ability rule (fixed 2026-10-02):** a 3rd-person challenge (trash collection) counts as exploring, so every ability works. 1st-person minigames block movement abilities. Level-wide zone pollution drains HP but no longer halves stamina regen; only local toxic pockets do that.

**Floating interaction prompts (2026-10-02):** the Talk/Interact prompt is a 3D icon with its verb centred underneath (`WorldInteractPrompt`). It always faces the camera, draws over scenery (`Lalim/UI/AlwaysOnTop` shader plus the TMP overlay shader, with templates in `Resources/WorldPrompt`) and can be tapped to interact. It sits above small targets, at the diver's eye level beside statues, and at `AreaStation.PromptAnchor` for stations. Any interactable can set its own spot with `IInteractionAnchor`. The GameHUD action button spawns it, so every scene with the HUD has it. Untick `_floatInWorld` on that button to go back to the HUD button.

**Re-running the builder:** Tools ▸ Lalim ▸ Level 1 ▸ Build Level Flow (`Editor/Level1SceneBuilder.cs`). It is idempotent: existing objects and hand edits are kept.

---

## 0. The pattern

Every zone runs the same loop. Level 2 and Level 3 will reuse it, so the code should be built around this loop rather than around Level 1's specific minigames.

```
 enter zone ──► zone is TOXIC (0.5 HP/s drain; paused in dialogue and 1P minigames)
     │
     ▼
 swim to statue ──► [chat icon] ──► dialogue (Dialogue HUD, game HUD hidden) ──► quest given
     │
     ▼
 for each minigame in zone:
     start* ──► auto perspective switch ──► minigame (DDA-tuned) ──► DDA re-scores player
     │                                                        │
     │                                   reward tool (toast) ◄┘  (tool feeds the NEXT minigame)
     ▼
 zone CLEANSED (HP drain stops, restoration visuals) ──► next barrier opens (banner, not toast)
```

\* **How a minigame starts:** in zone 1 the minigames chain automatically. Trash collection starts when the dialogue ends, and sorting starts when the last piece of trash is collected. In zones 2 and 3, the player swims to each area and taps the **interact icon** to start that area's minigame.

Patterns specific to Level 1:

- **Every output feeds the next input.** The trash you collect gets sorted. The sorting reward (Water Sampling Kit) is used for the readings. The readings reward (Coral Seed) is used for the replanting.
- **Three areas per zone.** In zone 2, the readings and the replanting reuse the same three areas. Zone 3 also has three net areas.
- **The 3rd person / 1st person split.** Swimming and searching happen in 3rd person, and precise work happens in 1st person. Only trash collection is a 3rd-person minigame.
- **The one-shot finale.** Level 1 ends when zone 3's cleanse opens the tunnel, which leads to the teleport scene, where Level 2 is unlocked.

| Zone | Statue | Minigames (in order) | Perspective | Requires | Reward |
|---|---|---|---|---|---|
| 1 | Amansinaya | Trash collection, then trash segregation | 3P, then 1P | — | **Water Sampling Kit** |
| 2 | Haik | Water level readings ×3 areas, then coral replanting ×3 areas | 1P at stations | Water Sampling Kit, then Coral Seed | **Coral Seed** (after readings) |
| 3 | Neguno | Ghost net untangling and removal ×3 areas | 1P at stations | — | Tunnel opens, then teleport, then Level 2 unlocked |

---

## 1. What already exists (reuse) vs. what's new

The new scene already has `GameManager`, `ChallengeManager`, `DialogueManager`, `LuzonReefQuestController`, `StatueDialogueRouter`, `SegregationAltar` (with three placeholder bowls), `OfferingItem`, `ActToastUI` (already using `achievement-title-toast.png`), `QuestObjectiveUI`, `Zone2_Barrier`, map bounds, checkpoints, `Player_final_final`, `PF_Dugong`, `GameHUD` and the `LuzonScene.fbx` environment. That environment has statue materials for amansinaya, haik and neguno.

| System | File(s) | Reuse plan |
|---|---|---|
| Challenge lifecycle and DDA metrics | `Mechanics/ChallengeManager.cs` | **Keep.** Extend the metrics (§4). |
| DDA engine | `DDA/DDAManager.cs`, `DDA/DDAConfig.cs`, `Data/DDAConfig.asset` | **Keep.** Add metrics, history blending and per-minigame profiles (§4). |
| 1P/3P camera | `Mechanics/CameraController.cs` (`SwitchMode`) | **Keep.** Make the perspective data-driven instead of "every challenge = 1P". |
| Statue dialogue | `Mechanics/StatueDialogueRouter.cs` | **Generalize.** One component per statue, used for all 3 statues. |
| Dialogue | `UI/DialogueManager.cs` | **Extend.** Add review, auto, speed, pause and HUD hiding (§3). |
| Trash collection | `Challenges/OfferingItem.cs`, `LuzonReefQuestController.cs` | **Keep the item.** Collection becomes a real 3P challenge so DDA scores it. |
| Segregation | `Challenges/SegregationAltar.cs` | **Keep.** Add DDA knobs. |
| Water sampling | `Challenges/SamplePoint.cs`, `WaterSamplingChallenge.cs` | **Keep the world station** (beacon, bubbles, readout). Replace hold-to-sample with the calibration puzzle. |
| Coral replanting | `Challenges/CoralSlot.cs`, `CoralReplantingChallenge.cs` | **Keep the slot and plant step.** Add a site-prep phase before it. |
| Toxic HP drain | `Abilities/ToxicWaterVolume.cs`, `ToxicWaterTracker.cs`, `Prefabs/Hazards/PF_ToxicWaterVolume.prefab` | **Keep as is.** The tracker already drops volumes that get disabled while the player is inside. |
| Zone visuals swap | `Mechanics/ZoneRestorationController.cs` | **Extend.** One per zone. Make the swap gradual instead of instant, and drive the level-wide cleanliness gradient (§2.8). |
| Zone/region progress | `Core/RegionProgressTracker.cs`, `GameManager.cs` | **Keep.** Completing all 3 `luzon_zone_*` IDs already publishes `RegionCompletedEvent`, and `GameManager` then unlocks Visayas. |
| Inventory and tools | `Inventory/*`, `Mechanics/ToolEquipSystem.cs`, `Data/Items/07_WaterSampleKit.asset` | **Keep.** Add a Coral Seed item. |
| Teleport | `Mechanics/RegionTeleporter.cs` in TeleportScene | **Keep.** Add a one-time "newly unlocked" reveal. |
| Interaction targeting | `PlayerController` raycast and `IInteractable` | **Keep.** Add the icon type and a HUD action button (§2.3). |
| Spirit Sense | `Mechanics/SpiritSense.cs`, `Abilities/SpiritGlow.cs`, `SpiritWisp.cs`, `HiddenObject.cs` | **Keep.** Wire it to the new `ObjectiveTracker`, add `SpiritGlow` to every target, and delete the `SpiritVision` stub (§2.6.3). |
| Audio navigation | `Mechanics/AudioNavigationManager.cs` (in the scene, unwired) | **Keep.** Wire its target, sources and clips, and add **new** proximity BGM (§2.6). |
| Checkpoints and saving | `Abilities/Checkpoint.cs`, `CheckpointManager.cs`, `Core/SaveManager.cs` | **Extend.** Save the checkpoint and scene, save when the app closes, auto-save new games, and resume mid-level (§2.7). |
| **Ghost net untangle** | — | **New** (§5.6). |
| **Calibration scanner UI** | — | **New** (§5.3). |
| **Coral site-prep (swipe/tap)** | — | **New** (§5.4). |
| **Level flow director** | — | **New.** Replaces the zone-1-only `LuzonReefQuestController` (§2.1). |
| **Dialogue HUD** | — | **New** (§3). |
| **Toast service and zone-unlock banner** | — | **New**, built from `ActToastUI` and `BarrierNotification` (§2.5). |

Don't use `ZoneTrigger` in the new scene. It publishes `ZoneCompletedEvent` after **each** challenge, so zone 2 would count as cleansed after the readings alone. It also draws a debug `OnGUI` overlay.

---

## 2. Core framework (build first; everything else plugs into it)

### 2.1 `LevelFlowDirector` + `ZoneQuest` + quest steps (new, `Scripts/Mechanics/Quest/`)

This generalizes `LuzonReefQuestController` into a data-driven sequence:

```
LevelFlowDirector (one per level scene)
 └─ ZoneQuest[] (ordered: Amansinaya, Haik, Neguno)
     ├─ zoneId            (keep luzon_zone_a/b/c so existing saves and RegionProgressTracker work)
     ├─ displayName       ("Amansinaya's Shallows", …)
     ├─ statue            (StatueDialogueRouter)
     ├─ toxicVolume       (ToxicWaterVolume, Light)
     ├─ restoration       (ZoneRestorationController)
     ├─ entryBarrier      (barrier blocking THIS zone; null for zone 1)
     └─ steps[]           (ordered IQuestStep components)
```

```csharp
public interface IQuestStep {
    string StepId { get; }                 // e.g. "luzon_z2_water" — saved in completedChallengeIds
    string ObjectiveText { get; }          // shown in QuestObjectiveUI
    void Begin(QuestContext ctx, System.Action onComplete);
    void RestoreCompleted();               // apply end-state instantly when loading a save
}
```

Step types:

| Step | Purpose |
|---|---|
| `TalkStep` | Turns on the statue's chat icon, plays its dialogue, completes when the dialogue ends. |
| `MinigameStep` | Publishes `ChallengeStartedEvent`. Completes on `ChallengeCompletedEvent` for its `ChallengeId`, and for multi-area minigames when all N areas report done. A `startMode` field decides how it starts: **`Auto`** starts the moment the previous step completes (zone 1), and **`InteractMarker`** places an interact-icon marker at each area and starts when the player taps it (zones 2 and 3). |
| `GrantItemStep` | Calls `InventoryManager.AddItem`, then shows a "Tool received" toast. |
| `CleanseZoneStep` | Disables the toxic volume, plays the restoration, publishes `ZoneCompletedEvent` once per zone. |
| `UnlockBarrierStep` | Dissolves the next barrier, shows the zone-unlock banner and points Spirit Sense at the next statue. |

Zone 1 steps: `Talk`, `Minigame(TrashCollection, Auto)`, `Minigame(Segregation, Auto)`, `GrantItem(WaterSampleKit)`, `CleanseZone`, `UnlockBarrier(Zone2)`.
Zone 2 steps: `Talk`, `Minigame(WaterReadings ×3)`, `GrantItem(CoralSeed)`, `Minigame(CoralReplant ×3)`, `CleanseZone`, `UnlockBarrier(Zone3)`.
Zone 3 steps: `Talk`, `Minigame(GhostNet ×3)`, `CleanseZone`, `UnlockBarrier(Tunnel)`. The tunnel's end trigger then calls `LevelExit`.

On load, the director walks the steps. Any step whose `StepId` is in `SaveData.completedChallengeIds` gets `RestoreCompleted()`, and the first incomplete step gets `Begin()`. This gives free resume mid-level.

### 2.2 Data-driven perspective

- Add `public CameraController.CameraMode Perspective;` to `ChallengeStartedEvent` (`Core/GameEvents.cs`), defaulting to first person.
- `CameraController.OnChallengeStarted` uses `evt.Perspective` instead of always switching to 1P.
- `PlayerController`: when the active challenge is 3P, keep normal exploration movement. The `_isInChallenge` branches currently disable auto-swim and force facing. Add `ChallengeManager.ActivePerspective` and gate those branches on it.
- **Granularity decision:** each 1P station is its own challenge, for example `luzon_z2_water_a/b/c`. Swimming between areas stays plain 3P exploration. This gives DDA a fresh reading after every area, so difficulty adapts area to area. It also avoids switching perspective inside a single challenge.
- Keep the camera's BehindBack style untouched (see memory note). Only the 1P/3P mode changes.

### 2.3 Interaction icons (chat / interact)

- New optional interface: `IInteractionIcon { InteractionIcon Icon { get; } }` with `enum InteractionIcon { Interact, Chat }`. `StatueDialogueRouter` returns `Chat`, and everything else defaults to `Interact`.
- New `HUD/ActionButton` added to `GameHUD.prefab` **surgically**. Add a child object and don't rebuild the prefab, because the user hand-edits the HUD. It holds:
  - an icon `Image`, swapped between `Sprites/UI/Game Mechanics/icon_chat.png` and `icon_interact.png`
  - a TMP label with the verb: "Talk", "Pickup", "Start", "Tap to use Water Sampling Kit"
  - an `onClick` that triggers the same path as `InputManager.InteractPressed`
- `HUDController.ShowInteractionPrompt(text)` gets an icon overload. `PlayerController.UpdateInteractionPrompt` already calls it. Remove or guard the debug `OnGUI` prompt in `PlayerController` (around line 518) so there's no duplicate.
- Optional: a small world-space floating icon over the target (`UI/FloatingUI.cs` exists) so players see the chat bubble above a statue before they're in range.

### 2.4 Zone toxicity (HP drain)

- One `PF_ToxicWaterVolume` per zone, sized to the zone's swimmable volume, with severity `Light`.
- **Drain rate: 0.5 HP per second in Level 1** (confirmed). The current `toxicLightDpsNormal` is 3 HP/s. At 100 HP, 0.5 HP/s takes about 200 s to kill an idle player.
- **Difficulty scaling happens per level, not live** (confirmed):
  - New `LevelToxicity` settings (a small section in `AbilityConfig`, or its own ScriptableObject). It holds a base drain per level (Level 1 = 0.5 HP/s) and a min/max multiplier range for scaling Level 2 and later.
  - When a level scene loads, `LevelFlowDirector` takes **one snapshot** of `DDAManager.DifficultyLevel` and computes `levelDps = baseDps(level) × multiplier(snapshot)`. Every zone volume in the level uses that value until the level ends. Mid-level DDA changes still tune the minigames, but they never touch the drain.
  - **Level 1 is a fixed 0.5 HP/s**, because the player has no performance history yet. Its multiplier is pinned to 1.0.
  - Save the snapshot in `SaveData` (`levelToxicDps`) so reloading mid-level keeps the same drain.
  - The existing live path (`ToxicWaterVolume.GetDps` → `AbilityConfig.GetToxicDamageMultiplier(AbilityDifficulty.Current)`) changes with every DDA update. Level volumes bypass it and read `levelDps` from the director. Other hazards keep the old behaviour.
- `CleanseZoneStep` disables the volume. `ToxicWaterTracker.PublishIfChanged` already drops disabled volumes, and `ToxicScreenEffect` turns off with it.
- Zones 2 and 3 are behind barriers, so their volumes only matter once the player gets in. That matches the spec.
- **The drain pauses during dialogue and during 1P minigames** (confirmed). Add `ToxicWaterTracker.Suppress(object source)` and `Release(object source)`; while any source holds it, `Update` skips the damage. `DialogueManager` suppresses between `DialogueStartedEvent` and `DialogueEndedEvent`. `ChallengeManager` suppresses while a challenge whose perspective is first person is active. Trash collection (3P) does **not** suppress it, so the drain keeps running while the player searches.

### 2.5 Toasts vs. banners

`achievement-title-toast.png` is used **only** for:

1. Entering a new level (this already works on scene load through `ActToastUI`)
2. Receiving a reward tool
3. Completing a quest or zone, and completing the level
4. Achievements

Work:

- Refactor `ActToastUI` into a `ToastService` with a queue: `Show(ToastKind kind, string title, string body, Sprite icon = null)`. It waits while dialogue is open and never overlaps two toasts. Keep `_playOnStart` for the level-entry toast.
- **Zone unlock uses a different, lighter banner.** Generalize `BarrierNotification` into `ZoneBanner`: a text-only strip such as "Haik's waters are open", without the toast sprite. It also handles "barrier still locked" bumps.
- Achievements: add a minimal `AchievementService` with an ID list, a save list (`SaveData.unlockedAchievementIds`) and a toast on unlock. Each achievement unlocks once per save. Level 1 set (confirmed):

  | ID | Name | Unlocks when |
  |---|---|---|
  | `l1_clean_sweep` | **Clean Sweep** | Trash sorting finishes with 0 wrong-bowl errors |
  | `l1_steady_hands` | **Steady Hands** | All 9 calibration meters (3 per area × 3 areas) lock without the needle ever leaving the band after first entering it |
  | `l1_net_breaker` | **Net Breaker** | Any ghost-net area is cleared without using a hint |
  | `l1_untouched` | **Untouched** | A zone is cleansed while the player is above 80% HP (it checks at the `CleanseZoneStep`) |

  The achievement checks listen to `ChallengeCompletedEvent` (error and hint counts) and `ZoneCompletedEvent` (HP). Calibration needs one extra per-meter flag from `CalibrationScannerUI`.

### 2.6 Navigation: one objective, three layered guides

The player always has one "where do I go next?" target. Three systems read it, and each answers a different question:

| Guide | Answers | Already exists? |
|---|---|---|
| **Proximity BGM** | "Am I getting closer?" Music gets louder near the target and quieter far from it. | **New** |
| **Spirit Sense** (active ability, 1 orb) | "Show me." Spirit sight, a glow on relevant targets, and a guiding wisp. | `Mechanics/SpiritSense.cs`, `Abilities/SpiritGlow.cs`, `SpiritWisp.cs`. Present in the scene through `[AbilitySystems]` and `Btn_SpiritSense`. |
| **Audio compass and escalating hints** | "Which way?" and "I'm stuck." A passive ping that's louder when facing the target, then an ambient cue, a distinct ping and a voice line as idle time grows. | `SpiritSense` compass and `Mechanics/AudioNavigationManager.cs`. In the scene, but **unwired**: `_navigationTarget`, its audio sources and clips are all empty. |

#### 2.6.1 `ObjectiveTracker` (new, `Mechanics/ObjectiveTracker.cs`): the single source of truth

- `SetObjective(Transform t)`, `SetObjectiveSet(IEnumerable<Transform> targets)` (it then tracks the **nearest unfinished** one), and `Clear()`. It publishes `ObjectiveChangedEvent`.
- `ObjectiveTracker` forwards the current target to `SpiritSense.SetObjective` and `AudioNavigationManager.SetNavigationTarget`, so they never drift apart. It also exposes `CurrentTarget` and `DistanceToTarget` for the BGM. Today, `LuzonReefQuestController.SetSpiritObjective` talks to Spirit Sense directly. That call moves into the tracker.
- Each quest step declares its objective. The director applies it when the step begins:

  | Step | Objective |
  |---|---|
  | Swim to a statue (before each `TalkStep`) | The statue |
  | Trash collection | **Set:** the remaining trash items (nearest one) |
  | Trash sorting (1P) | Cleared. Navigation guides are off in 1P minigames. |
  | Water readings / coral replanting / ghost nets | **Set:** the unfinished area markers in that zone (nearest one) |
  | Between finishing a minigame and the next barrier | The newly opened barrier gap, then the next statue |
  | After zone 3 is cleansed | The tunnel entrance, then the tunnel's end |

#### 2.6.2 Proximity BGM (new, `Core/ProximityMusic.cs`)

- One looping BGM `AudioSource` (2D, with `AudioChannelSource = Music`, so the Settings music slider still applies).
- Volume follows the distance to `ObjectiveTracker.CurrentTarget`:
  `t = 1 − clamp01((d − nearDist) / (farDist − nearDist))`, then `vol = lerp(minVol, maxVol, curve(t))`.
  - Defaults: `nearDist` 8 m, `farDist` 120 m, `minVol` 0.25 (**never silent**, so the level still has music), `maxVol` 1.0, and an ease-in curve so the last stretch swells noticeably.
- The volume is smoothed over about 1.5 s (`Mathf.MoveTowards`), so a dash or a new objective never makes the music jump.
- When the objective changes, the volume eases to the new distance. A soft chime marks the change, so the player knows the target moved.
- **Optional, recommended:** a second "arrival" stem (a shimmer or choir layer) that fades in only within about 15 m. It makes "you're here" unmistakable.
- **Context rules:**
  - **Dialogue:** music ducks to 40%.
  - **1P minigames:** distance control is off and the music holds at a fixed minigame level. You're already at the target.
  - **No objective:** music holds at mid volume.
  - **Zone cleansed:** a short swell, then the zone's music switches from a "polluted" track to a "healed" track, if the art team supplies one.
- **Per-zone tracks:** `ZoneQuest` gets a `bgmPolluted` and an optional `bgmCleansed` clip. They crossfade over about 3 s when the player crosses into another zone.
- **Assets needed:** no BGM files exist yet (`Audio/Ambient` and `Audio/Voice` are empty). Ship with one placeholder loop until the tracks arrive.

#### 2.6.3 Spirit Sense in Level 1 (existing ability: wire it, don't rebuild it)

- **Targets glow through `SpiritGlow` (`RelevanceMode.Auto`):** add it to every `OfferingItem`, area marker, `SamplePoint`, coral site and net area. Each one reports itself relevant until it's finished, so Spirit Sense lights only what's left to do.
- **Hidden trash:** at higher difficulty, part of the trash spawns with `HiddenObject` and only shows after a Spirit Sense pulse. This is the "hard-to-see share" knob in §4.3.
- **Wisp:** guides to `ObjectiveTracker`'s target. The DDA knob "wisp guides to the nearest item (on → off)" only affects trash collection.
- **Availability:**
  - Allowed while exploring and during trash collection.
  - Blocked during dialogue and during 1P minigames, using the existing `AbilityBlockReason.NotAllowedHere`. In 1P, hints come from the minigame's own hint button instead.
- **Teaching it:** Amansinaya's quest lines already say "Awaken the old sight… follow the light". The first time trash collection starts, pulse `Btn_SpiritSense` and show a one-time tooltip: "Tap to sense the sea's wounds". A save flag stops it repeating.
- **Lost players:** the existing link stays. When audio hints escalate to `DistinctPing`, Spirit Sense's cooldown is cut by `_lostPlayerCooldownCut`.
- **Cleanup:** delete the dead `SpiritVision` stub (`SetAvailable` does nothing) and the `[SpiritVision]` object in the scene, and remove the calls to it from `LuzonReefQuestController`.

#### 2.6.4 Escalating audio hints (existing `AudioNavigationManager`: wire it and fill it)

- `ObjectiveTracker` sets its target. Assign its three `AudioSource`s on `[AudioNavigationManager]` in the scene.
- "Progress" resets the idle timer. That covers `ItemCollectedEvent`, `ChallengeCompletedEvent` (already subscribed), `ObjectiveChangedEvent` (new) and getting 25% closer to the target (new, so a player who is swimming the right way is never nagged).
- Hints pause during dialogue and 1P minigames.
- The DDA already scales the tier delays (30, 60 and 90 s at Normal).
- **Assets needed:** an ambient cue, a ping clip, and 1–2 voice hint lines per zone in the statue's voice, for example Haik: "Seek the three places where the water speaks…".

### 2.7 Checkpoints and resuming after leaving the game

**What exists:** `Checkpoint` and `CheckpointManager` handle **respawn after death**. HP and orbs refill, and the level isn't reloaded. `SaveManager` auto-saves on progress events: zone or challenge completed, checkpoint reached, item collected. The progress saved includes `completedChallengeIds`, `completedZoneIds`, inventory, DDA and `lastRegion`.

**Gaps that stop a player who quits from resuming where they were:**

1. `SaveData` has **no checkpoint ID or scene**, so a reload can't put the player back where they were.
2. **No save when the app closes.** Nothing handles `OnApplicationPause` or `OnApplicationQuit`. On mobile, swiping the app away kills it without warning.
3. **New games never auto-save.** `AutoSave()` only writes once the session is bound to a slot, and a new game is only bound after a manual save from the pause menu (`SaveManager.BeginNewGame` says so). A player who never opens the pause menu loses everything on quit.
4. Progress inside a multi-part minigame isn't saved: trash collected so far, and which areas are done.

**Plan:**

- **SaveData additions:** `lastSceneName`, `lastCheckpointId`, `collectedTrashIds` (for the collection in progress), `levelToxicDps` (§2.4), `ghostNetBagState` (§5.6), `unlockedAchievementIds` (§2.5) and `tutorialFlags`. Each area is its own challenge (`luzon_z2_water_a`, …), so finished areas are already covered by `completedChallengeIds`.
- **Checkpoint placement in `02_Luzon`** (all `PF_Checkpoint`, `sectionId` = zone ID, for the DDA death-streak logic):

  | Zone | Checkpoints |
  |---|---|
  | 1 | Level spawn, Amansinaya statue, altar |
  | 2 | Just past Zone2_Barrier, Haik statue, areas A / B / C |
  | 3 | Just past Zone3_Barrier, Neguno statue, net areas A / B / C, tunnel entrance |

  Checkpoints are the spots the player swims through anyway. The area checkpoints double as the respawn point for that area's minigame.
- **When it saves:**
  - Reaching a checkpoint. This already triggers `AutoSave` through `CheckpointReachedEvent`, and `CheckpointManager` will now also write `lastCheckpointId` and `lastSceneName`.
  - Every finished quest step, which is already covered by `ChallengeCompletedEvent` and `ZoneCompletedEvent`.
  - Each trash pickup, throttled to one write per 5 s.
  - **New:** `OnApplicationPause(true)`, `OnApplicationFocus(false)` and `OnApplicationQuit` in `SaveManager`. These save immediately.
- **Never save in the middle of a 1P puzzle.** If the app closes during one, the save keeps the last checkpoint (the area's checkpoint), and that area's minigame restarts from the beginning on resume. Puzzles are short, so that's fair, and it avoids serializing puzzle state.
- **Resuming** (Main menu → Continue → `lastSceneName`):
  1. `LevelFlowDirector` replays the steps (§2.1). Completed steps apply their end state instantly: barriers open, zones cleansed, statues already talked to, tools in the inventory.
  2. The player spawns at `lastCheckpointId` through `CheckpointManager` (new `PlaceAtCheckpoint(id)`). HP and orbs are full, as on respawn.
  3. The current step re-begins. Trash collection only spawns items not in `collectedTrashIds`, and the counter continues. Station minigames show their interact marker again.
  4. `ObjectiveTracker` points at the current objective, so the BGM, Spirit Sense and the hints guide the player straight back in.
  5. A small "Progress restored" banner appears (not the toast sprite).
- **New-game auto-save (gap 3):** when a new game reaches the first Level 1 checkpoint, bind the session to the preferred slot (`SaveToSlot(preferredSlot)`). The pause menu's save button still lets players choose a different slot. **Confirmed.** This replaces the old "nothing is written until the player saves" behaviour for new games.
- **Death during a 1P minigame:** the challenge fails (DDA records it as a failure, as it already does), the player respawns at that area's checkpoint and the interact marker reappears.

### 2.8 Pollution you can see: a level-wide cleanliness gradient

All three zones start **polluted and toxic**, and the environment has to show it, not just the HP bar. Each cleared zone makes the **whole level** gradually cleaner, so it looks noticeably different at 0, 1, 2 and 3 zones cleared.

**Two layers, both gradual:**

1. **Level-wide `Cleanliness`, from 0 to 1.** It equals cleared zones ÷ 3, so the targets are 0, 0.33, 0.67 and 1.0. When a zone is cleansed, the value **tweens** to the new target over about 8 s, with an ease-in-out. It never snaps.
2. **Per-zone restoration:** `ZoneRestorationController`. The cleared zone gets its own stronger, local transformation: debris and dead coral fade or sink, and living coral, fish and seagrass grow in. The rest of the level only gets the level-wide improvement.

**New `Mechanics/PollutionDirector.cs`** (one per level scene, listens to `ZoneCompletedEvent`) drives everything from `Cleanliness`:

| What | Polluted (0) | Clean (1) | How |
|---|---|---|---|
| Fog | Murky green-brown, dense (about 0.035) | Clear blue (the scene's current `0, 0.35, 0.65`, density 0.015) | Lerp `RenderSettings.fogColor` and `fogDensity` |
| Colour grading | Desaturated, green/brown tint, low contrast | Neutral and vivid | A `PollutedLook` URP Volume (Color Adjustments, Split Toning, Vignette) with `weight = 1 − Cleanliness`, layered over the existing `UnderwaterEffectsVolume` |
| Sunlight and caustics | Dim, weak caustics, god rays off | Bright, strong caustics, god rays on | Directional light intensity, `CausticsAnimator` strength, god-ray alpha (respects the `AmbientEffect` quality tier) |
| Suspended murk | Thick floating particles: brown silt and plastic flecks | Thin plankton sparkle only | Emission rate on a new `PF_MurkParticles`, and on `PF_PlanktonParticles` |
| Fish life | A few lone fish | Schools return | `WanderingMarineAnimal` spawners enable more agents at each threshold (0.33, 0.67, 1.0) |
| Coral colour | Bleached white and grey | Full colour | A `_Bleach` material property (MaterialPropertyBlock) on reef renderers. Uncleared zones stay more bleached than cleared ones (combined with the zone's local state). |
| Ambient sound | Low drone, muffled | Reef crackle, whale-song hints | Crossfade the ambience loops (ties in with §2.6.2's per-zone BGM) |

**Rules:**

- **A cleared zone looks clean, and the rest of the level looks a bit better.** Each renderer group's look = the level-wide cleanliness, plus a bonus if its own zone is cleared. So zone 1 after clearing is fully vivid, while zones 2 and 3 are still murky, just less so.
- **Toxicity stays separate:** the HP drain is per zone (§2.4), and the visual gradient is level-wide. Clearing zone 1 doesn't stop zone 2's drain, but zone 2's water does look a little less grim.
- **Make it a moment:** when a zone is cleansed, the gradient starts **during** the restoration VFX, with a 2–3 s slow camera pan across the zone, so the player sees the change happen.
- **Resuming:** on load, `PollutionDirector` applies the saved cleared-zone count instantly, with no tween.
- **Performance:** everything is a lerp of a handful of global values. There are no per-frame material swaps. Particle and fish counts respect the `AmbientEffect` quality tier on weaker phones.
- **Art needed:** a polluted variant of each zone's set dressing (garbage piles, bleached coral, algae-covered rock) under `ZoneRestorationController`'s broken container, plus the restored variants. Placeholder: reuse the trash, rock and coral models already in `Models/`, with bleached material instances.

---

## 3. Dialogue mode and Dialogue HUD

### 3.1 Behaviour

- `DialogueManager.StartDialogue` publishes `DialogueStartedEvent`, and `EndDialogue` publishes `DialogueEndedEvent`. Add both events to `GameEvents.cs`.
- `HUDController` hides the whole `GameHUD` while dialogue is open: `CanvasGroup` alpha 0 with `blocksRaycasts` false, rather than `SetActive`, so layout state survives. Joystick, abilities, stamina and HP all disappear.
- The dialogue panel's background uses `Sprites/UI/Dialogue Boxes/Long Dialogue Box.png` (9-sliced).

### 3.2 Dialogue HUD buttons (new `UI/DialogueHUD.cs`)

| Button | Behaviour |
|---|---|
| **Review** | Opens a scrollable log of every line shown **so far in this scene**, never future lines. `DialogueManager` keeps a `List<DialogueLine> _history` and appends a line when it starts displaying. The log is cleared on `SceneLoadedEvent`. While the log is open, typing and auto-advance pause. |
| **Auto** | Toggle. After a line finishes typing, waits `autoDelayBase + chars × autoDelayPerChar` (scaled by speed), then advances. Tapping the dialogue box still works. |
| **Fast forward** | Cycles **1× → 1.2× → 1.5× → 2× → 1×**. Multiplies the typewriter rate and shortens the auto delay. It does **not** advance lines. The current speed shows on the button label. |
| **Pause** | Freezes the typewriter coroutine, the auto timer and tap-to-advance, and shows a "Paused" badge. Tapping Pause again resumes. It does **not** open the pause modal and does not touch `Time.timeScale`, since dialogue already uses realtime waits. |

Implementation notes:

- `TypeSentence` has to become pause-aware and speed-aware. Replace `WaitForSecondsRealtime(_typewriterSpeed)` with a loop that accumulates `unscaledDeltaTime × speed` and skips accumulation while paused.
- **Bug to prevent:** `DialogueManager.Update` advances on any `GetMouseButtonDown(0)`, so tapping a HUD button would also skip a line. Guard it with `EventSystem.current.IsPointerOverGameObject()`, including the touch-ID overload for mobile. Better still, make the dialogue box itself the advance button.
- Migrate the dialogue texts from legacy `Text` to `TMP_Text`, matching the rest of the HUD.
- Persist the Auto and Speed choices in `GameSettings` so they carry across dialogues.
- **Icons (confirmed: placeholders for now):** TMP glyph buttons: `☰` Review, `A` Auto (highlighted when on), `»` plus the speed label (`1×`, `1.2×`, `1.5×`, `2×`) for Fast forward, and `❚❚` / `▶` for Pause. Check that each glyph exists in the project's TMP font asset, and fall back to text labels if it doesn't. Each button keeps an empty `Image` slot, so the final sprites (planned for `Sprites/UI/Dialogue Boxes/`) can be dropped in later without code changes.

### 3.3 Statues

`StatueDialogueRouter` is generalized and used for all three statues:

- Serialized fields: `speakerName`, `portrait`, `questLines` and `idleLines` (for re-talking after the quest is given).
- "Gives quest during dialogue": the `TalkStep` completes when the quest dialogue ends. If a mid-dialogue beat is wanted (for example the quest objective appearing as the line is spoken), add an optional `DialogueLine.eventKey` that fires `DialogueBeatEvent`, and let the step listen for it.
- **Remove the zone-1 turn-in dialogue and the `SetTurnInReady` / `onTurnInComplete` path** (confirmed). Segregation starts automatically when the last piece of trash is collected (§5.2). Rewrite Amansinaya's quest lines so they explain both tasks up front, for example "…gather every wound from my shallows, then lay each in its rightful bowl." Also drop the hard-coded "twenty-five", because the count now comes from DDA.
- The scripts for all three statues are in §3.4, based on the research there.

### 3.4 Lore research and statue scripts

#### Research summary

| Deity | What the sources say | How the game uses it | Caveats |
|---|---|---|---|
| **Amansinaya** | Tagalog. Recorded in the **Boxer Codex** (1590s) and a **1613 Tagalog vocabulary**. Patron of fishermen, said to have **invented fishing gear**. Before casting a net or line, fishermen **whistled and prayed** to her: *"Kasumpâ akó; nawáỳ diriyán"* ("I am your sworn friend; let it be there"). The **first catch (*sinaya*) was not eaten**, because taking it would empty future catches. Food was offered so the being would do no harm. | Zone 1. The goddess whose gift (fishing gear) has turned into waste. Her ethic ("give back the first catch, take only what you need") is the game's sustainability message. She grants the Water Sampling Kit. | Her gender is debated. Early sources may mean "father of Sinaya", and a misread Spanish *-a* ending probably made her feminine. The game keeps the popular goddess depiction, as the existing statue and art do. The popular "Bathala vs Aman Sinaya, the first humans from bamboo" creation story traces to Marvel's 2009 *Encyclopaedia Mythologica*, **not** precolonial sources, so the scripts don't use it. |
| **Haik** | Tagalog. Recorded in the **Boxer Codex** (1590s, also spelled *Hayc*; "Saik" is a later transcription error). A sea being **invoked for protection at sea and from storms**. Modern retellings describe a kind god who guides voyagers past storms and waves, once loved and now nearly forgotten. | Zone 2. The navigator god. "Reading the water" is the readings minigame, his storm is invisible pollution, and he grants the Coral Seed. | Modern lists call him Aman Sinaya's "successor". That isn't in the primary sources, so the scripts don't claim it. |
| **Neguno** | The "sagacious ruler of the sea" in the shark-origin myth collected in Damiana Eugenio's *Philippine Folk Literature: The Myths*. **Kablay**, a greedy owner of fishing boats, rented them to poor fishers at ruinous rates. During a famine he told a begging old man to "jump into the sea". The old man was Neguno. A storm destroyed Kablay's house, and Kablay was turned into **the first shark**. | Zone 3. Greed in the sea, which is exactly what ghost nets are (gear abandoned by overfishing that keeps killing). His justice opens the tunnel. | The sources I found don't pin down which ethnic group the myth comes from, so the scripts just call him a sea ruler. |

Sources: [Aswang Project: Amansinaya](https://aswangproject.com/amansinaya) · [Aswang Project: Tagalog deities](https://www.aswangproject.com/tagalog-deities-in-philippine-mythology) · [Wikipedia: List of Philippine mythological figures](https://en.wikipedia.org/wiki/List_of_Philippine_mythological_figures) · [PhSpirits: Neguno](https://phspirits.com/neguno/) · [Philippine Mythology blog: Legend of the Sharks](https://philippinemytholgy.blogspot.com/2009/03/legend-of-sharks.html)

#### Script conventions

- `{player}` is replaced with `SaveData.playerName` when the line is shown. Player lines use `isPlayer = true`.
- Lines stay under about 170 characters so they fit `Long Dialogue Box.png` on a phone without scrolling.
- Each statue has: **quest** (a `TalkStep`), optional **mid-quest barks** (short dialogues triggered by steps), and **closing** (plays after the zone's last minigame, before the reward toast and the barrier opening). After that, re-talking plays one **idle** line.

#### Amansinaya (zone 1)

**Quest** (trash collection starts the moment it ends):
1. **Amansinaya:** Hush, little diver. Long ago the fisherfolk whistled to me before they cast their nets, and I answered them with silver shoals.
2. **Amansinaya:** "Kasumpâ akó," they would say. "I am your sworn friend." And they never ate the first catch. They gave it back, so the sea would give again.
3. **{player}:** The water stings… and everything here is so grey.
4. **Amansinaya:** Now the land sends down what it no longer wants. Plastic, glass, metal. My shallows choke on it, and the water has turned to poison.
5. **Amansinaya:** Gather every piece you find. Leave nothing behind, not one shard.
6. **Amansinaya:** If your eyes fail you, awaken the spirit sight. What is hidden will glow, and a light will lead you to it.
7. **Amansinaya:** Then lay each piece in its rightful bowl. What is sorted truly can be made new again. Go, and be quick. The poison does not wait.

**Bark: altar rises** (after the last trash pickup, before sorting):
1. **Amansinaya:** Every wound lifted. Now: plastic, glass, metal. Choose with care. The sea remembers.

**Closing** (then the Water Sampling Kit toast, the cleanse and the Haik barrier):
1. **Amansinaya:** Do you feel it? My shallows taste sweet again.
2. **Amansinaya:** Take this. It reads the water's hidden moods: its sourness, its salt, its warmth.
3. **Amansinaya:** Haik, who once guided sailors through every storm, waits beyond the next gate. His waters are sick in ways the eye cannot see.
4. **{player}:** I'll find him.
5. **Amansinaya:** Remember the fisherfolk's way, {player}. Take only what you need, and give back the first of what you are given.

**Idle:** *Amansinaya:* The fisherfolk will whistle for me again one day. Go on, Haik is waiting.

#### Haik (zone 2)

**Quest** (each reading area's interact marker appears when it ends):
1. **Haik:** A traveller… Few find their way to me now. Sailors once called my name before every voyage, and I calmed the storms in their path.
2. **Haik:** But no storm has frightened me like this one. It has no wind, no thunder. It hides inside the water itself.
3. **{player}:** Amansinaya gave me this kit. She said your waters are sick in ways I can't see.
4. **Haik:** Then read them the way a navigator reads the sky. Three places in my waters speak loudest. Test each one.
5. **Haik:** Healthy reef water is only a little bitter: a pH between 7.9 and 8.4. Its salt sits between 33 and 37 parts per thousand.
6. **Haik:** It is warm, but never hot: 24 to 29 degrees. Any warmer and the coral sickens and turns white.
7. **Haik:** Hold the needles steady, then tell me what is wrong. Only a true reading can guide a true course.

**Bark: readings done** (then the Coral Seed toast, then the replanting markers):
1. **Haik:** Now you see the storm. Sour water, wrong salt, fevered heat. No coral could live in this.
2. **Haik:** Take these seeds. Clear the dead ground at each place you tested, then plant them where the rock is clean.

**Closing** (then the cleanse and the Neguno barrier):
1. **Haik:** The young coral holds. One day fish will shelter here again, and sailors will follow them home.
2. **Haik:** Go deeper, traveller. Neguno rules the waters past the last gate. He is not as gentle as I am.

**Idle:** *Haik:* Steady hands, true readings. Neguno is waiting below.

#### Neguno (zone 3)

**Quest** (each net area's interact marker appears when it ends):
1. **Neguno:** So. A child of the land comes to my waters. Do you know what happened to the last one who came here only to take?
2. **{player}:** …No.
3. **Neguno:** His name was Kablay. He owned many boats, and rented them to poor fishers for more than they could ever pay.
4. **Neguno:** When famine came, an old beggar knocked on his door. Kablay told him to go and jump into the sea.
5. **Neguno:** The beggar was me. That night my storm took his house, and the sea took Kablay. He became the first shark.
6. **Neguno:** Kablay is gone, but his greed is not. Look: nets cast and abandoned, still fishing with no one left to haul them in.
7. **Neguno:** My creatures are tangled in them. Undo the knots, free every one, and pull those nets out of my sea.
8. **Neguno:** Do this, and I will open the deep path.

**Closing** (then the cleanse and the tunnel opening):
1. **Neguno:** Free. Every one of them. You came to give, not to take. That is rarer than pearls.
2. **Neguno:** The deep path is open. Swim down, {player}. Other waters are waiting for someone like you.

**Idle:** *Neguno:* The deep path is open. Do not keep it waiting.

#### Voice hint lines (§2.6.4, tier 3)

- **Amansinaya:** "Follow the glow, little diver. Every wound you leave keeps the poison alive."
- **Haik:** "Three places speak, traveller. Listen for the bubbles rising."
- **Neguno:** "My creatures are calling. Can you not hear them struggling?"

---

## 4. DDA: what changes

### 4.1 Signals (adding the "pickup rate")

`ChallengeManager` already tracks hesitation, errors and completion time against expected time. Add:

| New metric | Meaning | How |
|---|---|---|
| `FirstActionLatency` | "Pickup rate": how fast the player works out what to do | Time from challenge start to the first **correct** task action. `ReportTaskCompleted` and a new `ReportCorrectAction()` set it once. |
| `HintsUsed` | Reliance on help | Already counted. Add it to `ChallengeCompletedEvent`. |
| `ExpectedFirstAction` | Baseline per minigame | Comes from the profile (§4.3). |

- `DDAManager.ProcessChallengeMetrics` adds `firstActionScore` and `hintScore` with new weights in `DDAConfig`: `firstActionWeight` and `hintWeight`.
- Fix: `ChallengeManager` ignores `DDAConfig.hesitationThreshold` (there's an empty `if` block around line 135). Read it from config.
- Remove the orphan `PlayerMetricRecordedEvent("Luzon_CollectionTime")`. Collection becomes a real challenge, so it gets scored like the others.

### 4.2 "This and all prior minigames"

The exponential moving average already carries history, but only implicitly. Make it explicit and tunable:

- `DDAManager` keeps `List<float> _minigameScores`, saved in `SaveData.minigameScores`.
- Each challenge's score is blended before thresholding: `blended = lerp(currentScore, mean(allPriorScores), DDAConfig.historyWeight)`, with `historyWeight` at about 0.35.
- Log `[DDA] luzon_z2_water_b score=0.62 (raw 0.70, history 0.48) → difficulty 0.55` so it's visible in the F9 overlay (`Core/AbilityDebugOverlay.cs`).

### 4.3 Per-minigame difficulty profiles (new `DDA/MinigameDifficultyProfile.cs`, a ScriptableObject)

Each minigame has one asset with **Easy / Normal / Hard** parameter presets. The current `DDAManager.DifficultyLevel` (0 to 1) interpolates between them: 0 is Easy, 0.5 is Normal, 1 is Hard, using the same blend-through-1 approach as `DDAConfig`. Designers tune in the Inspector and never touch code. Each profile also carries `expectedSeconds` and `expectedFirstActionSeconds` for scoring.

| Minigame | Knobs (Easy → Hard) |
|---|---|
| Trash collection (3P) | Item count (12 → 25), share placed in hard-to-see spots (0% → 40%, Spirit Sense helps find them), Spirit Sense wisp guides to the nearest item (on → off) |
| Segregation (1P) | Item set (obvious items → ambiguous ones like a foil wrapper or a bottle with a cap), bowl labels shown (always → after first mistake → never), hints (5 → 1), wrong-bowl penalty (none → item returns to tray) |
| Water calibration (1P) | Green band width (35% → 12%), band drift speed, needle noise amplitude, lock hold time (2.0 s → 3.5 s), lock progress drain when you leave the band (slow → fast), meters at once (1 sequential → 3 sequential with faster drift) |
| Coral site prep (1P) | Algae coverage to clear (60% → 85%), number of bleached skeletons and taps per skeleton (2×3 → 5×6), decoy **living** coral that must **not** be chiseled (0 → 3, a critical-thinking error) |
| Ghost net untangle (1P) | **Trapped animals (= tangles) per area (3 → 7)**, knots per tangle (4 → 6, kept small because 7 tangles share one phone screen), edges per knot (sparse → dense), scramble strength, hints (3 → 1) |

The DDA step size (0.05) and smoothing are already configured. With 11 scored challenges in Level 1, a consistently strong or weak player drifts about 0.3 to 0.5, which is reasonable. Revisit after playtesting.

---

## 5. Minigames

### 5.1 Zone 1, minigame 1: Trash collection (3rd person)

- **Start: automatic.** The moment Amansinaya's quest dialogue ends, the step publishes `ChallengeStartedEvent { Type = TrashCollection (new enum value), Perspective = ThirdPerson, TaskQuantity = spawned count }`. There's no start button, and `QuestObjectiveUI` appears right away.
- **Spawning (all trash in the scene must be collected):** at the start, the step picks the DDA-adjusted number of spawn points from a larger authored pool and activates `OfferingItem`s there. **All unused pool items are destroyed**, so every piece of trash visible in the scene is part of the quest. The task count is exactly the number spawned. This replaces the fixed `_collectQuantityBase = 25` in `LuzonReefQuestController`.
- **Play:** each item shows the action button with the interact icon and the label **"Pickup"**. `QuestObjectiveUI` shows "Trash collected 3/18". Movement and abilities stay fully enabled, since this is exploration-style. The 0.5 HP/s drain keeps running.
- **Complete:** when the **last** piece is picked up, no trash remains in the scene. The challenge completes and DDA scores it.
- **Safety check:** if anything removes an item without collecting it (a respawn reset, a despawn), the remaining count is recomputed from the live items. That way the quest can never wait on trash that no longer exists.

### 5.2 Zone 1, minigame 2: Trash segregation (1st person)

- **Start: automatic, right after collection.** There's no return-to-statue step and no turn-in dialogue. When the last item is picked up, a short camera beat shows `Altar_AmanSinaya` rising from the sand. The player is moved to the altar's stand point, and the segregation challenge starts. The camera switches to 1P automatically through §2.2.
- **Play:** `SegregationAltar.Begin(collected)` already works with exactly the items the player collected (Plastic / Glass / Metal bowls). Replace the `*_PLACEHOLDER` bowl meshes with final art, and add the profile knobs.
- **Complete:** the challenge completes, then the steps `GrantItem(Water Sampling Kit)` → toast → `CleanseZone(zone 1)` → `UnlockBarrier(Zone2_Barrier)` → banner run in order. Spirit Sense then points at Haik.

### 5.3 Zone 2, minigame 1: Water level readings (1st person at each station, ×3)

1. **Probing and positioning (3P exploration):** three `SamplePoint` stations (areas A, B and C), each marked with the existing beacon and bubble column. Near one, the action button reads **"Tap to use Water Sampling Kit"** with the interact icon. The station requires `07_watersamplekit` in the inventory and auto-equips it.
2. **Calibration puzzle (1P, new `UI/CalibrationScannerUI.cs`):**
   - A scanner overlay shows three meters: pH, Salinity and Temperature. The water sampling kit model (`Models/tools/water sampling kit.fbx`) sits in the lower part of the 1P view as a held view-model.
   - Each meter's needle drifts (Perlin noise plus a sine wave) and a **green target band** moves along the track. The player drags or holds a stabilizer slider that counters the drift. While the needle stays inside the band, a lock ring fills, and it locks after the profile's hold time (default 3 s). Leaving the band drains the ring.
   - Meters run **in sequence**: pH, then salinity, then temperature. That suits a phone better than three at once.
   - DDA: every exit from the band after first entering it counts as `ReportError()`, and the first time the needle enters the band counts as `ReportCorrectAction()`.
3. **Result (changed 2026-10-02):** there's no diagnosis question. Each meter locks and shows its reading with **"Normal"**, for example "pH 8.1 · Normal". The area ends with a short summary of all three readings.
- After all 3 areas: `GrantItem(Coral Seed)` and a toast.

### 5.4 Zone 2, minigame 2: Coral replanting (1st person at the same 3 areas)

- **Start:** at each of the three areas from the readings, an interact marker appears: "Prepare the site".
- **Phase A — site prep (new `Challenges/CoralSitePrep.cs`):**
  - **Swipe** to brush away toxic algae. The algae is a mask texture over the rock: swipes raycast to UVs and paint the mask clear, and the cleared percentage is tracked (target from the profile).
  - **Tap repeatedly** to chisel dead, bleached skeletons. Each has tap HP, cracks, and shatters into particles.
  - At higher tiers, decoy **living** coral is mixed in. Tapping it counts as `ReportError()` and plays a flinch animation.
  - Once the site is clean, a healthy rock socket glows.
- **Phase B — plant:** reuse the `CoralSlot` hold-to-plant interaction with the **Coral Seed** model (`Models/tools/coral seed.fbx`). The coral then grows over a few seconds.
- **Item (confirmed): new Coral Seed, delete the Coral Replanting Kit.**
  - Create `Data/Items/09_CoralSeed.asset`: `itemId: 09_coralseed`, Tool type, with the model `Models/tools/coral seed.fbx`. Register it in `ItemDatabase.asset`.
  - Delete `Data/Items/05_CoralReplantingKit.asset` and its `.meta`, and remove its entry from `ItemDatabase.asset` (GUID `28d472f4bdfc6a54eac0cb60e595a1a0`, its only asset reference).
  - Code references to `05_coralreplantingkit` to replace with `09_coralseed`, or remove:

    | File | Line(s) | What |
    |---|---|---|
    | `Inventory/InventoryManager.cs` | 41 | `_starterItemIds` default. **Remove it entirely**, because a starter coral tool would skip the reward. Also check the serialized list on `[InventoryManager]` in the scene. |
    | `UI/InventoryUI.cs` | 162, 279, 458, 640, 675, 756 | Primary-tool order arrays, the debug "Add Starter Conservation Tools" button, and the description/label switch cases |
    | `Challenges/CoralSlot.cs` | 17, 27–28, 197 | `_requiredToolId` default, tooltip, and the "Equip Coral Replanting Kit" prompt |
    | `Challenges/WaterSamplingChallenge.cs` | 462, 569 | "Equip Coral Replanting Kit [2]" prompts. The new flow replaces these with the Coral Seed reward toast. |
    | `Mechanics/ZoneTrigger.cs` | 297, 320, 332 | Hotkey, tool-ID and name lookups (legacy, but it must still compile) |
    | `Editor/SceneManagersUtility.cs` | 310 | Editor builder that writes `_requiredToolId` |
    | `Core/GameEvents.cs`, `Mechanics/ToolEquipSystem.cs` | 111, 11 | Comments only |

  - Old saves that list `05_coralreplantingkit` in `collectedToolIds`: make `InventoryManager` skip unknown IDs with a warning rather than erroring. Check that it already does this.
- After all 3 areas: `CleanseZone(zone 2)` → `UnlockBarrier(Zone3_Barrier)` → banner. Spirit Sense then points at Neguno.

### 5.5 Zone 3: talk to Neguno, then ghost nets

Same `TalkStep` pattern as the other zones. Three net areas, each with an interact marker: "Untangle the net".

### 5.6 Zone 3 minigame: Ghost net untangling and removal (1st person, ×3) — new system

This follows the reference image. A real 3D net hangs in front of the 1P camera, with glowing **knots** (draggable nodes) joined by **ropes** (edges). Ropes that cross each other make tangles. The player drags knots until **no rope crosses another**. This is the classic *planarity / Untangle* puzzle, so it really is untangling, and it's always solvable.

**Files:** `Challenges/GhostNet/GhostNetPuzzle.cs` (controller), `NetGraphGenerator.cs` (pure C#, unit-testable), `NetKnot.cs`, `NetRope.cs`, `UI/FreedFriendsTray.cs`.

**Generation (guaranteed solvable):**

1. Lay out a **solved** graph first: N points on a jittered grid inside the net frame, connected by non-crossing edges. Start with a triangulation and remove edges until each knot has degree 2 to 4, keeping the graph connected. Store the solved positions.
2. Split the knots into **tangles** (clusters). Each tangle traps one animal, so the DDA animal count (3–7) sets the number of tangles. Tangles are placed in a loose grid across the net frame (for example 3 in a row, or 7 in a 4 + 3 layout) so each one has room to be dragged on a phone screen. A few "bridge" ropes link neighbouring tangles, as in the reference image. Bridge ropes are generated planar too, so they never make a tangle unsolvable.
3. Scramble the positions, for example onto a shuffled circle or with random swaps inside each tangle's region. Re-roll until crossings reach the profile's minimum.

**Presentation (3D, not flat UI):**

- Knots are emissive cyan spheres with a pulse on a curved plane about 1.5 m in front of the camera.
- Ropes are tube meshes (or a `LineRenderer` with a rope texture) with slight catenary sag, red like the reference. Crossing ropes glow brighter or tremble, and clear ropes relax and dim.
- The background is the actual `Models/nets/square-net.fbx` mesh draped between rocks, with the knot graph layered on top. Each trapped animal visibly struggles inside its tangle.

**Trapped animals (confirmed: 3–7 per area, small-to-mid sized, randomized, rarely repeating):**

- **Species pool:** these models from `Models/sea creatures + elements (fbx)` are small-to-mid sized: ClownFish, DoryFish, BrownFish, Crab, Lobster, StarFish, Octopus, Squid, Eel, Turtle and StingRay (**11 species**).
  - **Excluded as too large:** Whale, Hammerhead, TunaFish and Seal.
  - **Also excluded:** Penguin, which doesn't belong on a Philippine reef.
  - The pool lives in a `GhostNetSpeciesPool` ScriptableObject, so species can be added or removed in the Inspector. Each entry holds a prefab, a display name, a codex species ID, a tray icon and a scale that fits the animal inside a tangle.
- **Selection, a level-wide "shuffle bag":** the pool is shuffled once per level and animals are dealt from the bag in order. The bag only refills, re-shuffled, when it's empty.
  - **Within an area:** no repeats. Seven animals is fewer than the 11 species.
  - **Across the level:** all 11 species appear before any species repeats. Even the hardest case (7 + 7 + 7 = 21 animals) repeats each species at most twice.
  - When the bag refills, the first animal dealt can't match the last one dealt.
  - The bag's state is saved, so reloading mid-zone doesn't reshuffle animals that have already been seen.
- **Pairing animals with tangles:** larger animals (Turtle, StingRay, Octopus) get the larger tangle slots, and small ones (ClownFish, StarFish, Crab) get the smaller slots.

**Input:**

- Touch-drag a knot: raycast onto the puzzle plane and clamp to the frame. Crossings are tested in the plane's 2D local space using segment-intersection tests, at O(E²), which is fine for 10 to 30 edges.
- Each move that reduces crossings counts as `ReportCorrectAction()`. Moves that increase crossings don't count as errors, because exploration is fine. Hesitation is tracked as usual.

**Freeing:**

- When a tangle reaches zero crossings, its ropes slacken and fall away and the animal swims out of the net. It fills the next slot in the **Freed Ocean Friends** tray at the bottom of the screen. The tray shows one `?` slot per trapped animal in this area (3–7), and each slot reveals the animal's icon when it's freed.
- Publish `CodexEntryUnlockedEvent` for that species, which hooks into the existing codex modal.

**Removal:**

- Once every tangle in the area is free, a final **swipe up** hauls the loose net away. It dissolves into particles and the area turns clean.
- The challenge completes, and DDA scores it.

**Hints:** "Hints available: N" comes from the DDA hint budget. Using a hint snaps one crossed knot to its stored solved position with a gentle animation.

After all 3 areas: `CleanseZone(zone 3)` → `UnlockBarrier(TunnelBarrier)` → banner ("The deep path opens").

---

## 6. Level exit: tunnel to teleport scene

- `TunnelBarrier` (new) blocks the downward tunnel in zone 3. Add a soft current VFX and the Spirit Sense objective pointing down the tunnel.
- `LevelExitTrigger` (new) sits at the tunnel's end. It fades out, shows the "Level 1 complete" toast, then saves through `SaveManager`. The region is already marked complete by `RegionProgressTracker` → `RegionCompletedEvent` → `GameManager.UnlockNextRegion(Visayas)`. Finally it loads `TeleportScene` through `SceneLoader`.
- TeleportScene: `RegionTeleporter(Visayas)` already reads `IsRegionAccessible`. Add a one-time "unlocked" reveal: the fog clears, particles burst and a banner reads "Visayas teleport unlocked". Store it as a seen flag in `SaveData` (`seenUnlockReveal`).

---

## 7. Phased build order

Each phase ends playable. Tests go in EditMode under the **`Lalim.Tests`** group only, because the unfiltered run hangs on a URP test.

| Phase | Work | Done when |
|---|---|---|
| **P0 — scene prep** | Point `GameConstants.Scenes.LUZON` at `02_LuzonNew` and swap it into Build Settings, replacing `02_Luzon`. Add `DevBootstrapper` to the new scene, because `DDAManager` only lives in `00_BootSequence`, so pressing Play in this scene currently runs with no DDA. Block out the zone volumes, statue anchor points, `Zone3_Barrier`, `TunnelBarrier`, 3 area markers in each of zones 2 and 3, and all the checkpoints from §2.7. | You can swim all 3 zones with barriers in place, and the F9 overlay shows DDA. |
| **P1 — framework** | `PollutionDirector` and the level-wide cleanliness gradient (§2.8), `LevelFlowDirector`/`ZoneQuest`/steps (§2.1), perspective on the event (§2.2), action button and icons (§2.3), toxic zones (§2.4), toast service and banner (§2.5), `ObjectiveTracker`, proximity BGM with a placeholder loop, Spirit Sense and audio-hint wiring (§2.6). Port zone 1 onto the director with the existing collection and altar. | Zone 1 plays start to finish on the new framework: the kit toast fires and barrier 2 opens with the banner. Music swells near Amansinaya and the trash, and Spirit Sense lights only uncollected trash. |
| **P2 — dialogue** | Dialogue events, hiding the HUD, `DialogueHUD` (review/auto/speed/pause), the tap guard, the long dialogue box, generalized statues for all 3. | All three statues talk, and all 4 buttons work as described. |
| **P3 — DDA** | New metrics, history blend, `MinigameDifficultyProfile` assets for the 5 minigames, the hesitation-threshold fix. Tests for scoring and profile interpolation. | The F9 overlay shows per-challenge raw, history, blended and new difficulty. |
| **P4 — zone 2** | Calibration scanner, kit view-model, diagnostic readout with the "which reading is out of range?" question, Coral Seed item plus deleting the Coral Replanting Kit and its references (§5.4), site prep (swipe/tap/decoys), planting. Tests for the calibration lock logic. | Zone 2 is fully playable and cleanses, and barrier 3 opens. |
| **P5 — zone 3** | Ghost net generator, knots, ropes, freeing, the friends tray, removal swipe, hints. Tests: generator always yields a planar solution, the crossing detector, scramble ≥ minimum crossings. | Zone 3 is fully playable, animals are freed, and codex entries unlock. |
| **P6 — exit** | Tunnel barrier, exit trigger, level-complete toast, teleport reveal. | Completing zone 3 leads through the tunnel to TeleportScene, where the Visayas teleporter shows as unlocked. |
| **P7 — save/resume and polish** | Everything in §2.7: the new `SaveData` fields, checkpoint and scene saving, saving when the app closes, new-game auto-save binding, resuming through `LevelFlowDirector` and `CheckpointManager.PlaceAtCheckpoint`, the "Progress restored" banner, and death during a 1P minigame. Achievements. Final audio (BGM per zone, hint pings, voice lines). Mobile layout pass at 2532×1170. Tests: the `SaveData` round trip, and director replay from every step ID. | Kill the app (not just pause it) at every step, including mid-trash-collection and mid-puzzle. On relaunch, Continue puts the player at the right checkpoint with the right progress. |

Suggested order if time is short: P0 → P1 → P2 → P5 (ghost net is the riskiest new system) → P4 → P3 → P6 → P7.

---

## 8. Open questions

All design questions are answered; see **Decisions confirmed** at the top. Content still to come, none of which blocks the build:

- **Audio:** a BGM loop per zone (a polluted version, plus a healed version if possible), an optional "arrival" stem, an ambient hint cue, a hint ping, and 1–2 voice hint lines per statue. `Audio/Ambient` and `Audio/Voice` are empty today, so placeholders ship first.

- **Dialogue HUD icon art:** four sprites (Review, Auto, Fast forward, Pause) to replace the placeholder glyphs.
- **Dialogue review:** the §3.4 scripts are first drafts. Have someone who knows Tagalog check the fishermen's invocation and the honorifics.
- **Toxic drain for Level 2 and later:** the min/max multiplier range (§2.4). It isn't needed until Level 2.
- **Playtest tuning:** the 3–7 tangle layout on a 2532×1170 phone screen. If 7 tangles feel cramped, lower knots per tangle before lowering the animal count.

---

## 9. Known pitfalls

- `ZoneTrigger` marks a zone complete per challenge, so don't use it in the new scene (§1).
- `DialogueManager` advances on any click, so guard taps on HUD buttons (§3.2).
- `DDAManager` only exists when booted from `00_BootSequence`, so add `DevBootstrapper` to the new scene (P0).
- `GameHUD.prefab` is hand-edited. Add the action button and the hide-group surgically and never regenerate it.
- Keep the third-person camera **behind the back**. Only the 1P/3P mode is data-driven.
- The test runner saves dirty open scenes, so save `02_Luzon` deliberately before running tests.
- Keep zone IDs `luzon_zone_a/b/c` (map them to Amansinaya, Haik and Neguno), because `RegionProgressTracker` and `GameConstants.Zones.GetZonesForRegion` depend on them for unlocking Visayas.
- Don't let three systems each hold their own idea of "the objective". Only `ObjectiveTracker` sets targets on `SpiritSense` and `AudioNavigationManager` (§2.6.1).
- Unity's `OnApplicationQuit` isn't reliable on mobile when the app is swiped away. `OnApplicationPause(true)` is the save that actually lands, so test it on a device and not just in the Editor (§2.7).
- `OnTriggerExit` doesn't fire when a checkpoint or toxic volume teleports the player. `ToxicWaterTracker` already resets on respawn, so do the same when placing the player at a checkpoint on resume.
