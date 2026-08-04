# LALIM — Implementation Walkthrough

## What Was Built

All **26 foundational scripts** for the LALIM game have been created across 7 systems. Every script directory that was previously empty is now populated with production-ready code.

---

## Scripts by System

### Core (`Assets/Lalim/Scripts/Core/`) — 6 files

| Script | Purpose |
|--------|---------|
| [EventBus.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Core/EventBus.cs) | Type-safe static pub/sub — decouples all systems |
| [GameEvents.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Core/GameEvents.cs) | 20+ event structs organized by system |
| [GameConstants.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Core/GameConstants.cs) | Scene names, tags, layers, default values |
| [GameManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Core/GameManager.cs) | State machine + region unlock progression |
| [SceneLoader.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Core/SceneLoader.cs) | Async scene loading with progress + gating |
| [SaveManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Core/SaveManager.cs) | JSON persistence with backup + auto-save |

### Player & Mechanics (`Assets/Lalim/Scripts/Mechanics/`) — 6 files

| Script | Purpose |
|--------|---------|
| [InputManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/InputManager.cs) | Centralized input hub (joystick + swipe + buttons) |
| [PlayerController.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/PlayerController.cs) | Third-person movement with CharacterController |
| [CameraController.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/CameraController.cs) | Dual-perspective (3P orbit ↔ 1P free-look) with smooth transitions |
| [SpiritSense.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/SpiritSense.cs) | Audio compass (passive) + reveal pulse (active) |
| [AudioNavigationManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/AudioNavigationManager.cs) | 3-tier hint escalation (ambient → ping → voice) |
| [HiddenObject.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Mechanics/HiddenObject.cs) | Objects revealed by SpiritSense with fade-in |

### DDA Engine (`Assets/Lalim/Scripts/DDA/`) — 3 files

| Script | Purpose |
|--------|---------|
| [DDAConfig.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/DDA/DDAConfig.cs) | ScriptableObject with tuning parameters |
| [DDAManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/DDA/DDAManager.cs) | Heuristic scoring → difficulty adjustment → task/timer/hint scaling |
| [PlayerMetricsTracker.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/DDA/PlayerMetricsTracker.cs) | Real-time hesitation, error, and time tracking |

### Inventory (`Assets/Lalim/Scripts/Inventory/`) — 4 files

| Script | Purpose |
|--------|---------|
| [ItemData.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Inventory/ItemData.cs) | ScriptableObject for item definitions |
| [ItemDatabase.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Inventory/ItemDatabase.cs) | Registry with dictionary-cached lookups |
| [InventoryManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Inventory/InventoryManager.cs) | Dual sub-inventory (Relics + Tools) + key item tracking |
| [InteractableItem.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/Inventory/InteractableItem.cs) | World pickups with bob/rotate + collect VFX |

### UI (`Assets/Lalim/Scripts/UI/`) — 7 files

| Script | Purpose |
|--------|---------|
| [VirtualJoystick.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/UI/VirtualJoystick.cs) | On-screen joystick with dead zone |
| [TouchLookHandler.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/UI/TouchLookHandler.cs) | Swipe-to-look with invert-Y option |
| [UIManager.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/UI/UIManager.cs) | Panel stack (push/pop) + state-driven switching |
| [LoadingScreen.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/UI/LoadingScreen.cs) | Animated progress bar + random tips |
| [HUDController.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/UI/HUDController.cs) | SpiritSense cooldown, timer, hints, interaction prompts |
| [PauseMenu.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/UI/PauseMenu.cs) | Resume, Save, Settings, Quit |
| [RegionSelectUI.cs](file:///d:/UnityProj/Lalim_Capstone_Final/Assets/Lalim/Scripts/UI/RegionSelectUI.cs) | Three-region selector with lock/unlock states |

---

## Key Architecture Decisions

1. **EventBus over direct references** — All systems communicate via typed events. This means swapping or removing a system (e.g., DDA) won't break others.

2. **Struct events** — All event types are `struct` with a `where T : struct` constraint. This ensures zero GC allocations when publishing events — critical for mobile.

3. **ScriptableObject configs** — `DDAConfig`, `ItemData`, and `ItemDatabase` are all ScriptableObjects, giving designers Inspector-based tuning without code changes.

4. **Singleton pattern** — Core managers use `DontDestroyOnLoad` singletons to persist across scene loads. The pattern includes duplicate-prevention in `Awake()`.

5. **Dual-perspective camera** — `CameraController` uses `SmoothStep` lerp for the 3P↔1P transition, with obstacle avoidance raycast in third-person mode.

6. **DDA heuristic scoring** — Weighted combination of three metrics (hesitation, errors, time deviation) with exponential moving average smoothing to prevent difficulty from oscillating rapidly.

---

## Next Steps

To get these scripts running in Unity, you'll need to:

1. **Open the project in Unity** and let it compile all scripts
2. **Create a DDAConfig asset**: `Assets > Create > Lalim > DDA Config`
3. **Create ItemData assets**: `Assets > Create > Lalim > Item Data` (one per item)
4. **Create an ItemDatabase asset**: `Assets > Create > Lalim > Item Database`
5. **Set up the Boot Scene** (`00_BootSequence`):
   - Create a `GameObject` called `[GameManager]` with `GameManager`, `SceneLoader`, `SaveManager`, `DDAManager`, `PlayerMetricsTracker`, `InventoryManager`, `InputManager`, `UIManager` components
6. **Set up the Player prefab** with `PlayerController`, `CameraController`, `SpiritSense` components
7. **Build UI Canvases** for HUD, PauseMenu, LoadingScreen, RegionSelect, and wire them to `UIManager`
8. **Add scenes to Build Settings** in order: `00_BootSequence`, `01_MainMenu`, `02_Luzon`, `03_Visayas`, `04_Mindanao`

> [!TIP]
> You may also need to add `TextMeshPro` via the Unity Package Manager if it's not already installed — `LoadingScreen`, `HUDController`, `PauseMenu`, and `RegionSelectUI` use `TMP_Text`.
