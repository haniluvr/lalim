# Project Overview

- **Game Title:** Lalim
- **High-Level Concept:** An atmospheric underwater adventure and conservation RPG set in the Philippine seas (Luzon, Visayas, Mindanao) where players explore marine sanctuaries, deploy scientific conservation tools, harness indigenous and celestial ocean abilities (Spirit Sense & Moon Tide), and restore damaged reef ecosystems.
- **Players:** Single-player underwater exploration and challenge gameplay.
- **Inspiration / Reference Games:** *Abzû*, *Subnautica*, *Dave the Diver*, *Sky: Children of the Light*.
- **Tone / Art Direction:** Vibrant low-poly marine aesthetics with warm tropical sunbeams, bioluminescent deep waters, and clean, high-contrast cultural iconography.
- **Target Platform:** Mobile (iOS / Android) with Standalone Desktop (macOS / Windows) development support.
- **Screen Orientation / Resolution:** Landscape Only (Fixed Landscape Left / Landscape Right, Reference Resolution 1920×1080, responsive CanvasScaler).
- **Render Pipeline:** Universal Render Pipeline (URP - Medium Quality Preset on Unity 6000.5.6f1).

---

# Game Mechanics

## Core Gameplay Loop
1. **Underwater Navigation & Exploration:** The player navigates 3D ocean environments using fluid underwater buoyancy, diving to explore vibrant reefs, shipwrecks, and sea trenches.
2. **Abilities:**
   - **Spirit Sense:** Active pulse ability (with cooldown) revealing hidden marine objects, debris, and spatial audio cues pointing toward restoration objectives.
   - **Moon Tide:** Celestial tidal surge ability altering current dynamics and water clarity, enabling passage or clearing toxic suspensions.
3. **Conservation Challenges & Tools:** Entering degraded zones triggers timed restoration missions (Water Sampling, Coral Replanting, Waste Segregation) using specialized kits from the player's inventory.
4. **Codex & Knowledge Progression:** Discovering marine species and completing ecological challenges unlocks detailed Philippine Marine Codex entries, rewarding regional stewardship.

## Controls and Input Methods
- **Landscape Screen Layout:** Ergonomically designed for mobile thumb reach in dual-thumb landscape grip.
- **Movement Joystick (Bottom-Right):** Circular draggable virtual joystick (`VirtualJoystick`) driving 3D swimming translation (`InputManager.MoveInput`) and dynamic diver pitch/yaw tilting.
- **Touch Swipe Look (Left / Screen Body):** `TouchLookHandler` active on the touchable screen space outside UI controls, enabling smooth camera orbit and pitch swimming.
- **Quick-Access Action Cluster (Bottom-Right Arc):**
  - **Spirit Sense (Left of Joystick):** Instant thumb-tap with radial cooldown fill.
  - **Inventory (Above Joystick):** Instant tap opening the equipment and tools modal.
  - **Moon Tide (Diagonal Between Spirit Sense & Inventory):** Arc-positioned ability button with cooldown radial fill and tidal surge trigger.
- **Navigation & Lore Controls (Left Edge):**
  - **Navigation Button:** Opens the regional underwater map modal.
  - **Codex Button (Under Navigation):** Opens the Philippine Marine Species Codex modal.
- **System Settings (Top-Right):**
  - **Settings Button:** Pauses gameplay and opens the Settings / Pause menu.

---

# UI

## Landscape Layout Wireframe (1920 × 1080)

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [🧭 Nav/Map]                                                                   [⚙ Settings]      │
│                                                                                                  │
│ [📖 Codex]                                                                                       │
│                                                                                                  │
│                                                                                                  │
│                                                                   [🎒 Inventory]                 │
│                                                                        (-200, 330)               │
│                                                                                                  │
│   ◄─── Touch-Look Swipe Area ───►             [🌊 Moon Tide]                                     │
│        (Screen Left & Center)                     (-310, 270)                                    │
│                                                                                                  │
│                                        [✨ Spirit Sense]        [ ◎ VIRTUAL JOYSTICK ]          │
│                                           (-350, 180)                  (-200, 180)               │
│                                                                      (Outer: 180x180)            │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

## Detailed Element Positioning & Geometry (Anchored to 1920×1080)

1. **Top-Right Corner:**
   - **Settings Button (`Btn_Settings`):**
     - Anchor: Top-Right `(1, 1)`
     - Anchored Position: `(-80, -80)`
     - Dimensions: `74 × 74 px`
     - Visual: Custom image sprite slot (placeholder gear icon image).

2. **Left Edge (Stacked Navigation & Codex):**
   - **Navigation Button (`Btn_Navigation`):**
     - Anchor: Middle-Left `(0, 0.5)`
     - Anchored Position: `(80, 80)`
     - Dimensions: `80 × 80 px`
     - Action: Opens `MapModalUI` popup.
   - **Codex Button (`Btn_Codex`):**
     - Anchor: Middle-Left `(0, 0.5)`
     - Anchored Position: `(80, -30)` (directly underneath Navigation button)
     - Dimensions: `80 × 80 px`
     - Action: Opens `CodexModalUI` popup.

3. **Bottom-Right Corner (Movement & Action Arc):**
   - **Movement Joystick (`Joystick_Movement`):**
     - Anchor: Bottom-Right `(1, 0)`
     - Anchored Position: `(-200, 180)`
     - Background (Outer Circle): `180 × 180 px`, circular boundary image.
     - Draggable Handle (Thumb): `70 × 70 px`, centered circular image.
     - Script: `VirtualJoystick.cs` sending normalized `Vector2` to `InputManager.Instance.SetMoveInput()`.
   - **Inventory Button (`Btn_Inventory`):**
     - Anchor: Bottom-Right `(1, 0)`
     - Position: Directly on top of joystick at `(-200, 330)` (offset +150px Y)
     - Dimensions: `74 × 74 px`
     - Action: Calls `InventoryUI.Instance.ToggleInventory()`.
   - **Spirit Sense Button (`Btn_SpiritSense`):**
     - Anchor: Bottom-Right `(1, 0)`
     - Position: Directly to the left of joystick at `(-350, 180)` (offset -150px X)
     - Dimensions: `74 × 74 px`
     - Visual: Button image with child `Image` (radial 360 fill) showing active cooldown state.
     - Action: Calls `InputManager.Instance.TriggerSpiritSense()`.
   - **Moon Tide Ability Button (`Btn_MoonTide`):**
     - Anchor: Bottom-Right `(1, 0)`
     - Position: In between Spirit Sense and Inventory at `(-310, 270)` (~135° diagonal from joystick center)
     - Dimensions: `74 × 74 px`
     - Visual: Button image with radial cooldown fill.
     - Action: Triggers `MoonTideAbility.TriggerMoonTide()`.

4. **Modals & Overlays:**
   - **Map Modal (`MapModalUI`):** Full-screen dark marine translucent backdrop with centered regional map card, zone beacons, player coordinate tracker, and close [✕] button.
   - **Codex Modal (`CodexModalUI`):** Tabbed marine species gallery displaying Taklobo (Giant Clam), Butanding (Whale Shark), Coral Reefs, and Sea Turtles with scientific names, cultural lore, and unlock state.
   - **Image Slots Architecture:** All button game objects have public `Image` components configured with placeholder marine sprites and are ready for drag-and-drop replacement once the user uploads their final button image assets.

---

# Key Asset & Context

### Scripts Created / Modified
- `Assets/Lalim/Scripts/UI/HUDController.cs` (Modified):
  - Add serialized references for:
    - `_settingsButton`
    - `_navigationButton`
    - `_codexButton`
    - `_inventoryButton`
    - `_moonTideButton`
    - `_moonTideCooldownFill`
    - `_mapModal` (`MapModalUI`)
    - `_codexModal` (`CodexModalUI`)
  - Hook button listeners and wire up cooldown event updates.
- `Assets/Lalim/Scripts/Mechanics/MoonTideAbility.cs` (New):
  - Singleton ability script managing tidal phase triggers (`Low`, `Mid`, `High`), cooldown timers, and `TideChangedEvent` dispatching.
- `Assets/Lalim/Scripts/UI/MapModalUI.cs` (New):
  - Popup window displaying regional map, completed/active zone markers, and player position pin.
- `Assets/Lalim/Scripts/UI/CodexModalUI.cs` (New):
  - Popup window displaying unlocked Philippine marine species entries with descriptions and conservation badges.
- `Assets/Lalim/Scripts/UI/TouchLookHandler.cs` (Modified):
  - Ensure look drag region ignores pointer events over UI buttons and movement joystick using `EventSystem.current.IsPointerOverGameObject()`.

### Asset Placeholders & Prefabs Created
- `Assets/Lalim/Prefabs/UI/GameHUD.prefab`: Complete self-contained Canvas prefab with `CanvasScaler` configured for landscape (1920×1080), all 6 buttons, virtual joystick, and modal popups.
- `Assets/Lalim/Sprites/UI/PlaceholderIcons/`: Procedurally generated or styled 2D sprite placeholders (Settings, Map/Nav, Codex, Inventory, SpiritSense, MoonTide, JoystickBg, JoystickHandle) to provide immediate crisp visual feedback until custom uploaded images are linked.

---

# Implementation Steps

### Step 1: Create Moon Tide Ability & Mechanics Foundation
- **Description:** Create `Assets/Lalim/Scripts/Mechanics/MoonTideAbility.cs`. Implement tidal state cycling (`TideLevel.Low` -> `Mid` -> `High`), cooldown tracking, and event publishing (`TideChangedEvent`). Expose `TriggerMoonTide()` and public cooldown properties (`CooldownRemaining`, `CooldownTotal`, `IsOnCooldown`).
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

### Step 2: Create Map Modal & Codex Modal Components
- **Description:** 
  1. Create `Assets/Lalim/Scripts/UI/MapModalUI.cs` with open/close methods, zone marker indicators hooked to `RegionProgressTracker`, and close button listener.
  2. Create `Assets/Lalim/Scripts/UI/CodexModalUI.cs` with marine species entries, unlock states hooked to `CodexEntryUnlockedEvent` / save data, and close button listener.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

### Step 3: Enhance HUDController & Input Management
- **Description:** Update `HUDController.cs` to expose and wire all new buttons:
  - Settings Button -> open `PauseMenu` or publish `PauseRequestedEvent`
  - Navigation Button -> toggle `MapModalUI`
  - Codex Button -> toggle `CodexModalUI`
  - Inventory Button -> call `InventoryUI.Instance.ToggleInventory()`
  - Spirit Sense Button -> call `InputManager.Instance.TriggerSpiritSense()`
  - Moon Tide Button -> call `MoonTideAbility.Instance.TriggerMoonTide()`
  - Add Moon Tide cooldown fill update logic mirroring Spirit Sense cooldown.
- **Assigned role:** developer
- **Dependencies:** Steps 1, 2
- **Parallelizable:** No

### Step 4: Generate Clean UI Placeholder Sprites
- **Description:** Create an editor helper script or texture generator to produce crisp circular marine-themed placeholder sprites for:
  - `icon_settings.png`
  - `icon_navigation.png`
  - `icon_codex.png`
  - `icon_inventory.png`
  - `icon_spiritsense.png`
  - `icon_moontide.png`
  - `joystick_background.png`
  - `joystick_handle.png`
  Store in `Assets/Lalim/Sprites/UI/Placeholders/` with TextureImporter set to `Sprite (2D and UI)`.
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

### Step 5: Construct the GameHUD Prefab & Configure Hierarchy
- **Description:** Build `Assets/Lalim/Prefabs/UI/GameHUD.prefab`:
  - Canvas (Render Mode: Screen Space - Overlay, Pixel Perfect: true)
  - Canvas Scaler (Scale With Screen Size, Reference Resolution: 1920×1080, Screen Match Mode: Match Width Or Height, Match: 0.5)
  - Graphic Raycaster
  - `TouchLookHandler` covering background viewport
  - Top-Right: `SettingsButton` (Image + Button)
  - Left Edge: `NavigationButton`, `CodexButton` (stacked vertically)
  - Bottom-Right: `VirtualJoystick` (Circle Background + Thumb Handle)
  - Bottom-Right: `InventoryButton` (above Joystick)
  - Bottom-Right: `SpiritSenseButton` (left of Joystick) with radial cooldown image
  - Bottom-Right: `MoonTideButton` (diagonal between Spirit Sense & Inventory) with radial cooldown image
  - Modal Containers: `MapModal` (`MapModalUI`), `CodexModal` (`CodexModalUI`)
  - Attach `HUDController` and wire all serialized properties.
- **Assigned role:** developer
- **Dependencies:** Steps 3, 4
- **Parallelizable:** No

### Step 6: Integrate GameHUD into Scene Hierarchy and UIManager
- **Description:** Instantiate and wire `GameHUD` prefab into `02_Luzon.unity` and `00_BootSequence.unity` (`UIManager`). Ensure player controller movement seamlessly responds to the new bottom-right joystick and abilities fire as expected.
- **Assigned role:** developer
- **Dependencies:** Step 5
- **Parallelizable:** No

### Step 7: Configure PlayerSettings Orientation
- **Description:** Ensure Unity PlayerSettings are configured for Landscape Only (`defaultScreenOrientation = LandscapeLeft`, autorotation restricted to `LandscapeLeft` and `LandscapeRight`).
- **Assigned role:** developer
- **Dependencies:** None
- **Parallelizable:** Yes

---

# Verification & Testing

### 1. Touch & Joystick Controls Verification
- Run the scene in the Unity Editor or Game View set to 1920×1080 Landscape.
- Click and drag the bottom-right circular joystick: verify that handle stays bounded within the circle, and the diver accelerates and steers smoothly in 3D.
- Release joystick: verify handle snaps back to center and diver glides smoothly to rest.
- Drag in the left / center area of the screen: verify camera orbits smoothly via `TouchLookHandler` without triggering buttons.

### 2. Action Buttons Verification
- **Spirit Sense (Left of Joystick):** Click button; verify Spirit Sense pulse activates, audio cue plays, and radial cooldown fill counts down.
- **Moon Tide (Diagonal between Spirit Sense & Inventory):** Click button; verify `TideChangedEvent` publishes, console logs tidal shift, and cooldown indicator operates.
- **Inventory (Above Joystick):** Click button; verify inventory screen toggles open/closed.

### 3. Navigation & Codex Modal Verification
- **Settings (Top-Right):** Click; verify Pause / Settings menu displays.
- **Navigation (Left):** Click; verify Map modal opens showing regional points of interest; click [✕] to close.
- **Codex (Left, below Nav):** Click; verify Marine Codex modal opens showing species catalog; click [✕] to close.

### 4. Custom Image Replacement Check
- Verify that every button's `Image.sprite` property is cleanly exposed and documented so that when the user uploads their final button images, they can be assigned directly in the Inspector or via script with zero layout rework.
