# Graph Report - Core  (2026-10-08)

## Corpus Check
- Corpus is ~17,416 words - fits in a single context window. You may not need a graph.

## Summary
- 608 nodes · 1095 edges · 29 communities (23 shown, 6 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 34 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Settings and Presets
- Event Bus and Game State
- Save System
- Scene Loading Pipeline
- Audio Channels and Levels
- Core Namespace Files
- Music Director
- Debug Overlay and Ambient One-Shots
- Proximity Level Music
- SFX Cues
- Gameplay Events
- Quality-Tier Ambient Effects
- Game Constants
- Challenge Types
- Region Progress Tracking
- Shader Warmup and Tracing
- Ability Block Reasons
- Challenge Event Listeners
- Ability Ids and Events
- Zone Ambience
- Pause Clock
- Storage Tools
- Audio Hint Escalation
- Progress Events
- Item Types
- Toxic Exposure
- Damage Kinds
- Zone Completion Events
- Challenge Perspective

## God Nodes (most connected - your core abstractions)
1. `SaveManager` - 40 edges
2. `SettingsManager` - 35 edges
3. `Lalim.Core` - 28 edges
4. `GameManager` - 28 edges
5. `ProximityMusic` - 26 edges
6. `Region` - 23 edges
7. `GameSettings` - 23 edges
8. `GameAudio` - 22 edges
9. `MusicDirector` - 22 edges
10. `SceneLoader` - 21 edges

## Surprising Connections (you probably didn't know these)
- `AudioChannelSource` --references--> `AudioChannel`  [EXTRACTED]
  AudioChannelSource.cs → GameSettings.cs
- `GameAudio` --references--> `AudioLevels`  [EXTRACTED]
  GameAudio.cs → AudioLevels.cs
- `Bound` --references--> `AudioChannel`  [EXTRACTED]
  GameAudio.cs → GameSettings.cs
- `GameManager` --references--> `GameState`  [EXTRACTED]
  GameManager.cs → GameEvents.cs
- `GameManager` --references--> `Region`  [EXTRACTED]
  GameManager.cs → GameEvents.cs

## Import Cycles
- None detected.

## Communities (29 total, 6 thin omitted)

### Community 0 - "Settings and Presets"
Cohesion: 0.05
Nodes (23): PlayerDiedEvent, PlayerHealthChangedEvent, SettingsChangedEvent, GameSettings, CustomizePreset, EffectivePreset, LookMultiplier, TargetFrameRate (+15 more)

### Community 1 - "Event Bus and Game State"
Cohesion: 0.06
Nodes (23): EventBus, Scenes, GameState, Boot, Exploring, InChallenge, Menu, Paused (+15 more)

### Community 2 - "Save System"
Cohesion: 0.07
Nodes (17): SavedAbility, SaveData, SavedVitals, SaveManager, CurrentSave, CurrentSlot, HasCurrentSave, HasSavedThisSession (+9 more)

### Community 3 - "Scene Loading Pipeline"
Cohesion: 0.07
Nodes (15): SceneLoadProgressEvent, SceneLoadRequestedEvent, LoadingStages, Stage, FreeMemory, LoadData, WarmShaders, SceneLoader (+7 more)

### Community 4 - "Audio Channels and Levels"
Cohesion: 0.09
Nodes (12): AudioChannelSource, AudioLevels, Entry, Bound, GameAudio, MaxOneShots, Original, AudioChannel (+4 more)

### Community 6 - "Music Director"
Cohesion: 0.08
Nodes (12): MusicDirector, CurrentClip, Instance, Quitting, Mode, KeepPrevious, Level, Loading (+4 more)

### Community 7 - "Debug Overlay and Ambient One-Shots"
Cohesion: 0.08
Nodes (5): AbilityDebugOverlay, AmbientOneShots, Lalim.DevTools, DevBootstrapper, SceneSkipKey

### Community 8 - "Proximity Level Music"
Cohesion: 0.12
Nodes (8): DialogueEndedEvent, DialogueStartedEvent, ProximityMusic, Current, CurrentVolume, GuidePosition, Instance, Previous

### Community 9 - "SFX Cues"
Cohesion: 0.12
Nodes (13): Sfx, Library, SfxCue, AltarRise, Correct, ItemPickup, MinigameComplete, MinigameFailed (+5 more)

### Community 10 - "Gameplay Events"
Cohesion: 0.11
Nodes (15): CleanlinessChangedEvent, DifficultyChangedEvent, DugongMountChangedEvent, HintRequestedEvent, InventoryChangedEvent, ObjectiveChangedEvent, PlayerMetricRecordedEvent, PlayerRespawnedEvent (+7 more)

### Community 12 - "Game Constants"
Cohesion: 0.13
Nodes (11): Abilities, Audio, DDA, GameConstants, Layers, Player, Relics, Save (+3 more)

### Community 13 - "Challenge Types"
Cohesion: 0.13
Nodes (15): ChallengeType, CoralReplanting, FishGathering, FrequencyMatching, GhostNetRescue, HazardDefusal, PollutionScan, RitualOffering (+7 more)

### Community 15 - "Shader Warmup and Tracing"
Cohesion: 0.23
Nodes (3): ShaderWarmup, Tracer, Enabled

### Community 16 - "Ability Block Reasons"
Cohesion: 0.17
Nodes (12): AbilityBlockedEvent, AbilityBlockReason, AlreadyActive, AtFullHealth, Busy, Mounted, None, NotAllowedHere (+4 more)

### Community 17 - "Challenge Event Listeners"
Cohesion: 0.36
Nodes (4): ChallengeCompletedEvent, ChallengeFailedEvent, ChallengeStartedEvent, PauseRequestedEvent

### Community 18 - "Ability Ids and Events"
Cohesion: 0.20
Nodes (10): AbilityActivatedEvent, AbilityEndedEvent, AbilityId, BubbleBarrier, Riptide, SeagrassMend, SpiritSense, Surge (+2 more)

### Community 20 - "Pause Clock"
Cohesion: 0.22
Nodes (4): PauseClock, DeltaTime, Paused, Time

### Community 22 - "Audio Hint Escalation"
Cohesion: 0.25
Nodes (7): AudioHintEscalatedEvent, HintEscalationLevel, DistinctPing, None, SubtleAmbient, VoiceHint, SpiritSenseActivatedEvent

### Community 23 - "Progress Events"
Cohesion: 0.43
Nodes (6): CheckpointReachedEvent, CodexEntryUnlockedEvent, ItemCollectedEvent, RegionCompletedEvent, RegionUnlockedEvent, SceneLoadedEvent

### Community 24 - "Item Types"
Cohesion: 0.25
Nodes (8): ItemType, CompanionItem, ConsumableKit, QuestItem, Relic, Tool, ItemUsedEvent, ToolEquippedEvent

### Community 25 - "Toxic Exposure"
Cohesion: 0.40
Nodes (5): ToxicExposureChangedEvent, ToxicSeverity, Heavy, Light, None

### Community 26 - "Damage Kinds"
Cohesion: 0.50
Nodes (4): DamageKind, Contact, Other, Toxic

### Community 28 - "Challenge Perspective"
Cohesion: 0.67
Nodes (3): ChallengePerspective, FirstPerson, ThirdPerson

## Knowledge Gaps
- **151 isolated node(s):** `MinTier`, `MaxOneShots`, `Tags`, `Layers`, `DDA` (+146 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 236 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Lalim.Core` connect `Core Namespace Files` to `Settings and Presets`, `Save System`, `Scene Loading Pipeline`, `Music Director`, `Gameplay Events`, `Quality-Tier Ambient Effects`, `Game Constants`, `Zone Ambience`?**
  _High betweenness centrality (0.323) - this node is a cross-community bridge._
- **What connects `MinTier`, `MaxOneShots`, `Tags` to the rest of the system?**
  _151 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Settings and Presets` be split into smaller, more focused modules?**
  _Cohesion score 0.05273937532002048 - nodes in this community are weakly interconnected._
- **Why does `SettingsManager` connect `Settings and Presets` to `Core Namespace Files`, `Debug Overlay and Ambient One-Shots`?**
  _High betweenness centrality (0.124) - this node is a cross-community bridge._
- **Should `Event Bus and Game State` be split into smaller, more focused modules?**
  _Cohesion score 0.06274509803921569 - nodes in this community are weakly interconnected._
- **Why does `SaveManager` connect `Save System` to `Scene Loading Pipeline`, `Progress Events`, `Debug Overlay and Ambient One-Shots`?**
  _High betweenness centrality (0.121) - this node is a cross-community bridge._
- **Should `Save System` be split into smaller, more focused modules?**
  _Cohesion score 0.07346938775510205 - nodes in this community are weakly interconnected._