# LALIM — Complete 3 Challenges Setup Guide

This guide provides complete, step-by-step instructions for setting up all **3 challenges** in Unity from scratch:
1. **Waste Segregation Challenge** (Sorting trash into Plastic, Metal, General bins)
2. **Water Sampling Challenge** (Collecting pH, Salinity, and Temperature readings)
3. **Coral Replanting Challenge** (Gathering fragments and anchoring them into substrate slots)

You can build and test all three right now using placeholder primitives (cubes, spheres, cylinders) before the final 3D environment and models are complete.

---

## 1. Prerequisites & Core Setup Checklist

Before setting up the zones, verify these scene components and project settings:

### Player GameObject Requirements
* **Tag**: Must be tagged **`Player`** (*case-sensitive; [ZoneTrigger.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/ZoneTrigger.cs) and [SortingBin.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Challenges/SortingBin.cs) check `CompareTag("Player")`*).
* **PlayerController Component**:
  * **`_interactableLayer`**: Must be set to include the **`Interactable`** layer (Layer 3) or `Default`. **If left as `Nothing`, raycasts will hit 0 objects.**
  * **`_interactionRange`**: Default is `3.0` – `4.0` meters.
* **Controls**:
  * **Press `I`**: Open/Close Inventory & Equipment menu (equips Water Sample Kit, Coral Replanting Kit, etc.).
  * **Keys `1`–`3`**: Quick-equip hotkeys while Inventory is open.
  * **Press `E`**: Enter challenge zone trigger / Pick up debris / Collect coral fragment.
  * **Hold `E`**: Water sampling (hold for 2.5s) / Coral planting (hold for 3.0s).

### Required Scene Managers (Singletons)
Ensure these managers are present in the scene (can be created in 1 click via **`Tools > Lalim > Setup Scene Managers`**):
* **`[GameManager]`** with [GameManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Core/GameManager.cs)
* **`[ChallengeManager]`** with [ChallengeManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/ChallengeManager.cs)
* **`[InputManager]`** with [InputManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/InputManager.cs)
* **`[InventoryManager]`** with [InventoryManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Inventory/InventoryManager.cs)
* **`[ToolEquipSystem]`** with [ToolEquipSystem.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/ToolEquipSystem.cs)
* **`[InventoryUI]`** with [InventoryUI.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/UI/InventoryUI.cs)

---

## 2. Challenge 1: Waste Segregation Challenge

**Gameplay Flow**: The player swims into the zone trigger, presses `E` to start. The camera switches to 1st person. The player picks up scattered debris (`Plastic`, `Metal`, `Food`), swims into the corresponding sorting bin trigger volume, and drops it. The challenge completes when all debris items are sorted.

```text
Hierarchy Layout:
▼ WasteSegregation_Zone                 [WasteSegregationChallenge.cs]
   ├── ZoneTrigger_Entry               [BoxCollider (isTrigger), ZoneTrigger.cs]
   ├── ChallengeStartPoint             [Transform (optional player teleport)]
   ├── Bin_Plastic                     [BoxCollider (isTrigger), SortingBin.cs]
   ├── Bin_Metal                       [BoxCollider (isTrigger), SortingBin.cs]
   ├── Bin_Food                        [BoxCollider (isTrigger), SortingBin.cs]
   └── DebrisItems (Parent)
       ├── Debris_Bottle_01            [Collider (solid), DebrisItem.cs, Layer: Interactable]
       ├── Debris_Can_01               [Collider (solid), DebrisItem.cs, Layer: Interactable]
       └── Debris_Food_01              [Collider (solid), DebrisItem.cs, Layer: Interactable]
```

### Step-by-Step Setup

#### Step 1: Create Zone Root
1. In the Hierarchy, create an Empty GameObject named **`WasteSegregation_Zone`**.
2. Attach [WasteSegregationChallenge.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Challenges/WasteSegregationChallenge.cs).

#### Step 2: Create Zone Trigger Entrance
1. Create a child GameObject named **`ZoneTrigger_Entry`**.
2. Add a **Box Collider**:
   * Check **`Is Trigger = true`**.
   * Size the box large enough to act as the entrance gate (e.g., `X: 8, Y: 4, Z: 8`).
3. Add [ZoneTrigger.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/ZoneTrigger.cs):
   * **Zone Id**: `luzon_zone_a`
   * **Region**: `Luzon`
   * **Challenge Type**: `WasteSegregation`
   * **Base Task Quantity**: `6` to `9`
   * **Base Time Limit**: `120` seconds
   * **Expected Completion Time**: `90` seconds
   * **Interaction Prompt**: `Press E to begin Waste Segregation`
   * **Required Tool Id**: Leave empty `""` (or `04_wastesegregationbag` if bag is mandatory).

#### Step 3: Create the 3 Sorting Bins
Create 3 child GameObjects under `WasteSegregation_Zone`:
1. **`Bin_Plastic`**:
   * Visual: Add a placeholder cylinder/cube or bin 3D model.
   * Collider: Add a **Box Collider** or **Capsule Collider** and check **`Is Trigger = true`**.
   * Attach [SortingBin.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Challenges/SortingBin.cs):
     * **Accepted Category**: `Bottle`
     * **Bin Label**: `Plastic Bin`
     * **Bin Renderer**: Drag the MeshRenderer of the bin here (used for green/red flash feedback).
2. **`Bin_Metal`**:
   * Duplicate `Bin_Plastic`, name it `Bin_Metal`.
   * Set **Accepted Category**: `Metal`.
   * Set **Bin Label**: `Metal Bin`.
3. **`Bin_Food`**:
   * Duplicate `Bin_Plastic`, name it `Bin_Food`.
   * Set **Accepted Category**: `Food`.
   * Set **Bin Label**: `Organic / General Bin`.

#### Step 4: Create Debris Items
Create 6–10 debris items scattered across the seafloor:
1. Create a 3D model (or primitive shapes like small cylinders for cans/bottles, cube for food).
2. **Layer**: Set to **`Interactable`** (Layer 3).
3. **Collider**: Add a Box or Capsule Collider with **`Is Trigger = false`** (*must be solid for raycasting*).
4. Attach [DebrisItem.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Challenges/DebrisItem.cs):
   * **Category**: Set to `Bottle`, `Metal`, or `Food`.
   * **Debris Name**: e.g., `Plastic Bottle`, `Soda Can`, `Apple Core`.

#### Step 5: Wire the Inspector on `WasteSegregationChallenge`
Select the parent **`WasteSegregation_Zone`**:
* **Plastic Bin**: Drag `Bin_Plastic`.
* **Metal Bin**: Drag `Bin_Metal`.
* **General Bin**: Drag `Bin_Food`.
* **Preplaced Debris**: Lock the Inspector and drag all debris GameObjects into this list.

---

## 3. Challenge 2: Water Sampling Challenge

**Gameplay Flow**: Diver equips the Water Sample Kit (`07_watersamplekit` / Press `[1]`), swims to designated floating sample points at varying depths, aims at each point, and holds `E` for 2.5 seconds to collect water quality parameters (`pH`, `Salinity`, `Temperature`).

```text
Hierarchy Layout:
▼ WaterSampling_Zone_01                 [WaterSamplingChallenge.cs (_zoneId: luzon_zone_b)]
   ├── ZoneTrigger_Entry               [BoxCollider (isTrigger), ZoneTrigger.cs (_zoneId: luzon_zone_b)]
   ├── SamplePoint_pH_01               [SphereCollider (solid), SamplePoint.cs, Layer: Interactable]
   ├── SamplePoint_Salinity_01         [SphereCollider (solid), SamplePoint.cs, Layer: Interactable]
   └── SamplePoint_Temp_01             [SphereCollider (solid), SamplePoint.cs, Layer: Interactable]
```

### Multi-Zone Setup (1-Click or Manual)
* **1-Click Generation**: Go to **`Tools > Lalim > Create Water Sampling Zone (1-Click)`**.
  * Automatically assigns incremental names (`WaterSampling_Zone_01`, `02`, `03`...).
  * Automatically sets matching unique IDs (`luzon_zone_b`, `luzon_zone_b_2`, `luzon_zone_b_3`...).
  * Automatically offsets position in the water column so zones do not overlap.
* **Auto-Repair Existing Zones**: If you previously created multiple zones that share duplicate IDs or fail to trigger, click **`Tools > Lalim > Validate & Fix Zone IDs`** to instantly inspect and repair every zone in the scene.

### Post-Sampling Safety Pop-up Modal
When all sample points in a zone are successfully analyzed:
1. `WaterSamplingChallenge` publishes completion with its unique `_zoneId`.
2. A prominent oceanic modal appears on-screen:
   * **Header**: `🌿 WATER AREA RESTORED & SAFE FOR REPLANTING!`
   * **Scientific Status**: Verified pH (8.2), Salinity (35 PSU), and Temperature (27.5°C).
   * **Next Directive**: `Equip Coral Replanting Kit [Press 2] and proceed to Coral Replanting!`
   * **Dismissal**: Press `[Space]` or `[E]`, or click the continue button.

---

## 4. Challenge 3: Coral Replanting Challenge

**Gameplay Flow**: Diver equips the Coral Replanting Kit (`05_coralreplantingkit` / Press `[2]`), enters the degraded reef bed, picks up scattered coral fragments with `E`, swims to substrate slots, aims, and holds `E` for 3.0 seconds to anchor each fragment. The bare substrate plug transforms into healthy living coral foliage upon completion.

```text
Hierarchy Layout:
▼ CoralReplanting_Zone_01               [CoralReplantingChallenge.cs (_zoneId: luzon_zone_c)]
   ├── ZoneTrigger_Entry               [BoxCollider (isTrigger), ZoneTrigger.cs (_zoneId: luzon_zone_c)]
   ├── CoralSlot_01                    [SphereCollider (solid), CoralSlot.cs, Layer: Interactable]
   │   ├── SubstratePlug               [Cylinder MeshRenderer (bare plug)]
   │   └── Planted_SM_Plant_Coral_1    [Foliage Prefab Instance (starts inactive)]
   ├── CoralSlot_02 ...
   ├── CoralFragment_01                [SphereCollider, CoralFragment.cs, Miniature Coral Mesh]
   └── CoralFragment_02 ...
```

### Foliage Prefab Variations by Zone
The challenge utilizes the official ocean pack coral prefabs located at `Assets/Davis3D/LowpolyOceanPack/Prefabs/Foliage/`:
* **Zone 01**: `SM_Plant_Coral_1 Variant.prefab` (Table / branch coral)
* **Zone 02**: `SM_Plant_Coral_2 Variant.prefab` (Staghorn cluster coral)
* **Zone 03**: `SM_Plant_Coral_1B Variant.prefab` (Sub-variant vibrant coral)
* **Zone 04**: `SM_Plant_Coral_2B Variant.prefab` (Dense branch variant)

### 1-Click Generation (Recommended)
1. Go to **`Tools > Lalim > Create Coral Replanting Zone (1-Click)`**.
2. This automatically:
   * Detects existing zones and creates `CoralReplanting_Zone_01`, `02`, `03`...
   * Assigns unique IDs `luzon_zone_c`, `luzon_zone_c_2`, `luzon_zone_c_3`...
   * Selects the respective `SM_Plant_Coral` foliage variation for that zone.
   * Generates 4 substrate anchor slots with inactive coral instances ready to bloom.
   * Scatters 6 collectible coral fragments using scaled models of the variant.
   * Wires all slots and fragments into `CoralReplantingChallenge`.
   * Enforces tool requirement `05_coralreplantingkit`.

### Planting Feedback & Real-Time HUD
* **While Holding `[E]`**: A real-time green/cyan progress bar (`PLANTING CORAL FRAGMENT... XX%`) displays on screen.
* **On Completion**: The substrate plug deactivates, the full `SM_Plant_Coral` blooms into place, and task progress increments towards full reef restoration!

---

## 5. Environmental Transformation: Broken to Restored States

To visually transform an area from **broken/polluted** before the challenge to **healed/vibrant** after completion, use the provided [ZoneRestorationController.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/ZoneRestorationController.cs).

### The Two-Container Pattern

The cleanest workflow is grouping your environment art into two parent GameObjects under each zone:

```text
▼ WasteSegregation_Zone                 [WasteSegregationChallenge, ZoneRestorationController]
   ├── ZoneTrigger_Entry               [ZoneTrigger.cs]
   ├── Bins & Challenge Objects...
   │
   ├── [Environment_Broken]            <-- Active by default; disabled on completion
   │   ├── Bleached_Coral_Clusters     (Grey, dull, lifeless models)
   │   ├── Trash_Heaps_Static          (Background clutter, tangled nets)
   │   ├── Murky_Water_VFX             (Brown/green particulate fog)
   │   └── Oil_Slick_Decals
   │
   └── [Environment_Restored]          <-- Disabled by default; enabled on completion
       ├── Vibrant_Coral_Garden        (Lush, colorful corals, anemones)
       ├── Fish_Flock_Spawner          (Swimming tropical fish schools)
       ├── Clear_Sunrays_VFX           (Underwater caustics & bubbles)
       └── Bioluminescent_Flora        (Glowing sea plants)
```

### How to Set It Up in Unity:
1. Select your zone root GameObject (e.g., `WasteSegregation_Zone`).
2. Add the [ZoneRestorationController.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/ZoneRestorationController.cs) component.
3. Configure the fields:
   * **Zone Id**: Set to match your `ZoneTrigger._zoneId` (e.g., `luzon_zone_a`, `luzon_zone_b`, or `luzon_zone_c`).
   * **Broken State Container**: Drag in the `[Environment_Broken]` parent GameObject.
   * **Restored State Container**: Drag in the `[Environment_Restored]` parent GameObject.
   * **Restoration VFX**: (Optional) Assign a celebratory cleansing wave or bubble burst particle system.
   * **Restoration Fanfare**: (Optional) Assign a triumphant sound clip.
   * **Transition Delay**: Default `0.5` seconds (lets the fanfare/particle effect start before visuals swap).

### What Happens Automatically:
* **On Scene Start**: The script checks `RegionProgressTracker`. If the player already finished this zone in their save, it immediately shows the restored environment and hides the broken one.
* **On Challenge Complete**: When the player finishes the final task, `ZoneTrigger` fires `ZoneCompletedEvent`. The controller catches this event, plays the VFX & audio fanfare, hides the broken container, and activates the flourishing restored container.
* **Testing in Editor**: Right-click the `ZoneRestorationController` component in the Inspector to use **Preview Broken State** or **Preview Restored State** without entering Play Mode.

---

## 6. Quick Reference & Common Pitfalls

| Object Type | Component | Collider Setting | Layer | Interaction Method |
|:---|:---|:---|:---|:---|
| **Zone Entrance** | `ZoneTrigger` | `Is Trigger = true` | `Default` | Walk in + Press `E` |
| **Sorting Bins** | `SortingBin` | `Is Trigger = true` | `Default` | Swim into bin while carrying item |
| **Debris Item** | `DebrisItem` | `Is Trigger = false` (Solid) | `Interactable` | Aim + Press `E` |
| **Sample Point** | `SamplePoint` | `Is Trigger = false` (Solid) | `Interactable` | Aim + Hold `E` (2.5s) |
| **Coral Fragment** | `CoralFragment` | `Is Trigger = false` (Solid) | `Interactable` | Aim + Press `E` |
| **Coral Slot** | `CoralSlot` | `Is Trigger = false` (Solid) | `Interactable` | Aim + Hold `E` (3.0s) |

### Top 3 Debugging Tips
1. **Raycast hitting nothing when pressing E**: Check that the item's GameObject layer is set to `Interactable` AND that `_interactableLayer` on `PlayerController` has `Interactable` checked.
2. **Zone Trigger not detecting player**: Verify the player root GameObject has the `Player` tag applied.
3. **Bypassing tools for rapid test**: During prototyping, clear the `Required Tool Id` field on `ZoneTrigger`, `SamplePoint`, and `CoralSlot` to test the full gameplay loop immediately without having to equip items from the inventory first.

