# Graph Report - Core  (2026-10-08)

## Corpus Check
- 28 files · ~17,592 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 29 file(s) not represented in the graph (top: .meta 29)

## Summary
- 615 nodes · 1105 edges · 26 communities (22 shown, 4 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 34 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `c16e4fef`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- SettingsManager
- GameManager
- SaveManager
- SceneLoader
- GameAudio
- Lalim.Core
- MusicDirector
- MonoBehaviour
- ProximityMusic
- SfxCue
- GameEvents.cs
- AmbientEffect
- GameConstants
- ChallengeType
- GameState
- Tracer
- AbilityBlockReason
- AbilityId
- ZoneAmbience
- PauseClock
- StorageTools
- HintEscalationLevel
- ItemType
- ToxicSeverity
- DamageKind
- ChallengePerspective

## God Nodes (most connected - your core abstractions)
1. `SaveManager` - 40 edges
2. `SettingsManager` - 35 edges
3. `Lalim.Core` - 28 edges
4. `GameManager` - 28 edges
5. `ProximityMusic` - 27 edges
6. `Region` - 23 edges
7. `GameSettings` - 23 edges
8. `GameAudio` - 22 edges
9. `MusicDirector` - 22 edges
10. `SceneLoader` - 21 edges

## Surprising Connections (you probably didn't know these)
- `GameManager` --references--> `GameState`  [EXTRACTED]
  GameManager.cs → GameEvents.cs
- `AudioChannelSource` --references--> `AudioChannel`  [EXTRACTED]
  AudioChannelSource.cs → GameSettings.cs
- `GameAudio` --references--> `AudioLevels`  [EXTRACTED]
  GameAudio.cs → AudioLevels.cs
- `Bound` --references--> `AudioChannel`  [EXTRACTED]
  GameAudio.cs → GameSettings.cs
- `GameManager` --references--> `Region`  [EXTRACTED]
  GameManager.cs → GameEvents.cs

## Import Cycles
- None detected.

## Communities (26 total, 4 thin omitted)

### Community 0 - "SettingsManager"
Cohesion: 0.05
Nodes (23): PlayerDiedEvent, PlayerHealthChangedEvent, SettingsChangedEvent, GameSettings, CustomizePreset, EffectivePreset, LookMultiplier, TargetFrameRate (+15 more)

### Community 1 - "GameManager"
Cohesion: 0.05
Nodes (28): EventBus, Scenes, ChallengeCompletedEvent, ChallengeFailedEvent, ChallengeStartedEvent, CheckpointReachedEvent, CodexEntryUnlockedEvent, ItemCollectedEvent (+20 more)

### Community 2 - "SaveManager"
Cohesion: 0.07
Nodes (17): SavedAbility, SaveData, SavedVitals, SaveManager, CurrentSave, CurrentSlot, HasCurrentSave, HasSavedThisSession (+9 more)

### Community 3 - "SceneLoader"
Cohesion: 0.07
Nodes (15): SceneLoadProgressEvent, SceneLoadRequestedEvent, LoadingStages, Stage, FreeMemory, LoadData, WarmShaders, SceneLoader (+7 more)

### Community 4 - "GameAudio"
Cohesion: 0.09
Nodes (12): AudioChannelSource, AudioLevels, Entry, Bound, GameAudio, MaxOneShots, Original, AudioChannel (+4 more)

### Community 6 - "MusicDirector"
Cohesion: 0.08
Nodes (12): MusicDirector, CurrentClip, Instance, Quitting, Mode, KeepPrevious, Level, Loading (+4 more)

### Community 7 - "MonoBehaviour"
Cohesion: 0.08
Nodes (5): AbilityDebugOverlay, AmbientOneShots, Lalim.DevTools, DevBootstrapper, SceneSkipKey

### Community 8 - "ProximityMusic"
Cohesion: 0.11
Nodes (9): DialogueEndedEvent, DialogueStartedEvent, ProximityMusic, Current, CurrentVolume, GuidePosition, Instance, IsPlaying (+1 more)

### Community 9 - "SfxCue"
Cohesion: 0.09
Nodes (19): HintRequestedEvent, Sfx, Library, SfxCue, ActTitle, AltarRise, ButtonTap, Correct (+11 more)

### Community 10 - "GameEvents.cs"
Cohesion: 0.11
Nodes (15): CleanlinessChangedEvent, DifficultyChangedEvent, DugongMountChangedEvent, InventoryChangedEvent, LevelCompletedEvent, ObjectiveChangedEvent, PlayerMetricRecordedEvent, PlayerRespawnedEvent (+7 more)

### Community 12 - "GameConstants"
Cohesion: 0.13
Nodes (11): Abilities, Audio, DDA, GameConstants, Layers, Player, Relics, Save (+3 more)

### Community 13 - "ChallengeType"
Cohesion: 0.13
Nodes (15): ChallengeType, CoralReplanting, FishGathering, FrequencyMatching, GhostNetRescue, HazardDefusal, PollutionScan, RitualOffering (+7 more)

### Community 14 - "GameState"
Cohesion: 0.25
Nodes (8): GameState, Boot, Exploring, InChallenge, Menu, Paused, Results, GameStateChangedEvent

### Community 15 - "Tracer"
Cohesion: 0.23
Nodes (3): ShaderWarmup, Tracer, Enabled

### Community 16 - "AbilityBlockReason"
Cohesion: 0.17
Nodes (12): AbilityBlockedEvent, AbilityBlockReason, AlreadyActive, AtFullHealth, Busy, Mounted, None, NotAllowedHere (+4 more)

### Community 18 - "AbilityId"
Cohesion: 0.20
Nodes (10): AbilityActivatedEvent, AbilityEndedEvent, AbilityId, BubbleBarrier, Riptide, SeagrassMend, SpiritSense, Surge (+2 more)

### Community 20 - "PauseClock"
Cohesion: 0.22
Nodes (4): PauseClock, DeltaTime, Paused, Time

### Community 22 - "HintEscalationLevel"
Cohesion: 0.25
Nodes (7): AudioHintEscalatedEvent, HintEscalationLevel, DistinctPing, None, SubtleAmbient, VoiceHint, SpiritSenseActivatedEvent

### Community 24 - "ItemType"
Cohesion: 0.25
Nodes (8): ItemType, CompanionItem, ConsumableKit, QuestItem, Relic, Tool, ItemUsedEvent, ToolEquippedEvent

### Community 25 - "ToxicSeverity"
Cohesion: 0.40
Nodes (5): ToxicExposureChangedEvent, ToxicSeverity, Heavy, Light, None

### Community 26 - "DamageKind"
Cohesion: 0.50
Nodes (4): DamageKind, Contact, Other, Toxic

### Community 28 - "ChallengePerspective"
Cohesion: 0.67
Nodes (3): ChallengePerspective, FirstPerson, ThirdPerson

## Knowledge Gaps
- **156 isolated node(s):** `MinTier`, `MaxOneShots`, `Tags`, `Layers`, `DDA` (+151 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 241 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Lalim.Core` connect `Lalim.Core` to `SettingsManager`, `SaveManager`, `SceneLoader`, `MusicDirector`, `GameEvents.cs`, `AmbientEffect`, `GameConstants`, `ZoneAmbience`?**
  _High betweenness centrality (0.325) - this node is a cross-community bridge._
- **What connects `MinTier`, `MaxOneShots`, `Tags` to the rest of the system?**
  _156 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `SettingsManager` be split into smaller, more focused modules?**
  _Cohesion score 0.05273937532002048 - nodes in this community are weakly interconnected._
- **Why does `SettingsManager` connect `SettingsManager` to `Lalim.Core`, `MonoBehaviour`?**
  _High betweenness centrality (0.123) - this node is a cross-community bridge._
- **Should `GameManager` be split into smaller, more focused modules?**
  _Cohesion score 0.050957481337228175 - nodes in this community are weakly interconnected._
- **Why does `SaveManager` connect `SaveManager` to `GameManager`, `SceneLoader`, `MonoBehaviour`?**
  _High betweenness centrality (0.119) - this node is a cross-community bridge._
- **Should `SaveManager` be split into smaller, more focused modules?**
  _Cohesion score 0.07346938775510205 - nodes in this community are weakly interconnected._