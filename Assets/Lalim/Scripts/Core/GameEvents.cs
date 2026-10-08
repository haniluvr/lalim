using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// All game event structs used by the EventBus.
    /// Events are value types (structs) to avoid GC allocations.
    /// </summary>

    // ─────────────────────────────────────────────
    // Game State
    // ─────────────────────────────────────────────

    public enum GameState
    {
        Boot,
        Menu,
        Exploring,
        InChallenge,
        Paused,
        Results
    }

    public struct GameStateChangedEvent
    {
        public GameState PreviousState;
        public GameState NewState;
    }

    // ─────────────────────────────────────────────
    // Scene Management
    // ─────────────────────────────────────────────

    public struct SceneLoadRequestedEvent
    {
        public string SceneName;
        public bool ShowLoadingScreen;
    }

    public struct SceneLoadProgressEvent
    {
        public float Progress; // 0.0 to 1.0
    }

    public struct SceneLoadedEvent
    {
        public string SceneName;
    }

    /// <summary>
    /// SaveManager.CurrentSave was replaced (a save was loaded, or a new game started). The
    /// persistent systems (inventory, zone progress, DDA) reload from it so nothing carries over.
    /// </summary>
    public struct SaveDataReplacedEvent { }

    // ─────────────────────────────────────────────
    // Challenge System (Perspective Switching)
    // ─────────────────────────────────────────────

    public enum ChallengeType
    {
        // Shared (appear in multiple regions)
        WasteSegregation,       // Sort trash into correct bins
        WaterSampling,          // Test pH/salinity/temp at marked locations
        CoralReplanting,        // Anchor coral fragments to substrate

        // Luzon-specific
        PollutionScan,          // Use Spirit Sense to find hidden pollution zones
        RitualOffering,         // Deliver offering at shrine

        // Visayas-specific
        SustainableFishing,     // Line tension + species ID mini-game
        GhostNetRescue,         // Cut nets to free trapped wildlife

        // Mindanao-specific
        HazardDefusal,          // Locate and defuse underwater explosives
        FrequencyMatching,      // Sound-match to calm whale shark
        TrenchDescent,          // Manage pressure during deep dive

        // Level 1 (Luzon) additions — appended so serialized enum values stay stable
        TrashCollection,        // 3rd-person swim-and-pickup before segregation

        // Level 2 (Visayas) — appended for the same reason
        FishGathering,          // Caluya: call scattered herds with the Radiant Conch, avoid predators
        SelectiveCatching,      // Ajuy: net targets, spare protected species
        SchoolRelocation        // Padre Burgos: bring the schools to sanctuaries while Bakunawa hunts
    }

    /// <summary>Camera perspective a challenge runs in. FirstPerson is the default (value 0).</summary>
    public enum ChallengePerspective
    {
        FirstPerson,
        ThirdPerson
    }

    public struct ChallengeStartedEvent
    {
        public ChallengeType Type;
        public string ChallengeId;
        public int TaskQuantity;       // Adjusted by DDA (e.g., number of trash items)
        public float TimeLimit;         // Adjusted by DDA (seconds, 0 = no timer)
        public int HintBudget;          // Adjusted by DDA
        public ChallengePerspective Perspective; // Default FirstPerson
    }

    public struct ChallengeCompletedEvent
    {
        public string ChallengeId;
        public ChallengeType Type;
        public bool Success;

        // Performance metrics for DDA
        public float HesitationTime;
        public int ErrorCount;
        public float CompletionTime;
        public float ExpectedTime;      // Baseline for deviation calculation

        // "Pickup rate": how quickly the player worked out what to do
        public float FirstActionLatency;    // seconds from start to the first correct action (-1 = none)
        public float ExpectedFirstAction;   // baseline for the above (0 = unknown)

        // Reliance on help
        public int HintsUsed;
        public int HintBudget;
    }

    public struct ChallengeFailedEvent
    {
        public string ChallengeId;
        public ChallengeType Type;
        public string Reason;           // "timeout", "too_many_errors", etc.
    }

    // ─────────────────────────────────────────────
    // Inventory
    // ─────────────────────────────────────────────

    public enum ItemType
    {
        Relic,              // Cultural artifacts (Radiant Conch, Woven Net Fragment, Tide Lens)
        Tool,               // Reusable conservation tools (Water Sample Kit, Coral Seed)
        ConsumableKit,      // Single-use items (Waste Segregation Bag)
        QuestItem,          // Key progression items (Recycled Coral Stone, Ritual Offering)
        CompanionItem       // Dugong-related (Bioluminescent Pearl)
    }

    public struct ItemCollectedEvent
    {
        public string ItemId;
        public ItemType Type;
        public string RegionOrigin;
    }

    public struct ItemUsedEvent
    {
        public string ItemId;
        public ItemType Type;
    }

    public struct InventoryChangedEvent
    {
        public int TotalRelics;
        public int TotalTools;
        public int TotalQuestItems;
    }

    /// <summary>A relic was equipped or unequipped from the inventory (its HUD button appears / goes).</summary>
    public struct RelicEquipChangedEvent
    {
        public string ItemId;
        public bool Equipped;
    }

    /// <summary>The player tapped an equipped relic's HUD button (conch call, net throw…).</summary>
    public struct RelicUsedEvent
    {
        public string ItemId;
    }

    /// <summary>
    /// An equipped relic's HUD button (or its key) went down or came back up. Relics used by
    /// holding (the Woven Net Fragment: hold to aim, release to throw) listen to this.
    /// </summary>
    public struct RelicHoldEvent
    {
        public string ItemId;
        public bool Down;
    }

    // ─────────────────────────────────────────────
    // Tool Equip
    // ─────────────────────────────────────────────

    public struct ToolEquippedEvent
    {
        public string ItemId;
        public ItemType Type;
    }

    public struct ToolUnequippedEvent
    {
        public string ItemId;
    }

    // ─────────────────────────────────────────────
    // DDA (Dynamic Difficulty Adjustment)
    // ─────────────────────────────────────────────

    public struct DifficultyChangedEvent
    {
        public float DifficultyLevel;           // 0.0 (easiest) to 1.0 (hardest)
        public float TaskQuantityMultiplier;     // e.g., 1.3 = 30% more items
        public float TimerStrictness;            // 0.0 (generous) to 1.0 (tight)
        public int HintBudget;                   // Number of hints available
        public int PuzzleComplexityTier;         // 0 = simple, 1 = medium, 2 = complex
    }

    public struct PlayerMetricRecordedEvent
    {
        public string MetricName;
        public float Value;
    }

    // ─────────────────────────────────────────────
    // SpiritSense
    // ─────────────────────────────────────────────

    public struct SpiritSenseActivatedEvent
    {
        public Vector3 PlayerPosition;
        public float RevealRadius;
    }

    public struct SpiritSenseRevealEvent
    {
        public GameObject RevealedObject;
        public string ObjectId;
    }

    // ─────────────────────────────────────────────
    // Audio Navigation
    // ─────────────────────────────────────────────

    public enum HintEscalationLevel
    {
        None,
        SubtleAmbient,      // Level 1: soft directional cue
        DistinctPing,        // Level 2: clear audio ping
        VoiceHint            // Level 3: narrated guidance
    }

    public struct AudioHintEscalatedEvent
    {
        public HintEscalationLevel Level;
        public Vector3 TargetPosition;
    }

    // ─────────────────────────────────────────────
    // Region / Progression
    // ─────────────────────────────────────────────

    public enum Region
    {
        Luzon,
        Visayas,
        Mindanao
    }

    public struct RegionUnlockedEvent
    {
        public Region UnlockedRegion;
    }

    public struct RegionCompletedEvent
    {
        public Region CompletedRegion;
    }

    // ─────────────────────────────────────────────
    // Zone Completion
    // ─────────────────────────────────────────────

    public struct ZoneCompletedEvent
    {
        public string ZoneId;
        public Region Region;
        public ChallengeType ChallengeType;
    }

    // ─────────────────────────────────────────────
    // Marine Species Codex
    // ─────────────────────────────────────────────

    public struct CodexEntryUnlockedEvent
    {
        public string SpeciesId;
        public string RegionDiscovered;
    }

    // ─────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────

    public struct PauseRequestedEvent
    {
        public bool IsPaused;
    }

    public struct HintRequestedEvent
    {
        public int RemainingHints;
    }

    // ─────────────────────────────────────────────
    // Abilities (Surge, Riptide, Spirit Sense, Tidebond, Seagrass Mend, Bubble Barrier)
    // ─────────────────────────────────────────────

    public enum AbilityId
    {
        Surge,
        Riptide,
        SpiritSense,
        Tidebond,
        SeagrassMend,
        BubbleBarrier
    }

    public enum AbilityBlockReason
    {
        None,
        NotEnoughStamina,
        OnCooldown,
        NotAllowedHere,
        AlreadyActive,
        Busy,           // e.g. Surge while Riptide is running, or mid-dash
        NotCharged,     // charge-based skill (Bubble Barrier) isn't full yet
        AtFullHealth,
        Mounted,
        NotMounted
    }

    public struct AbilityActivatedEvent
    {
        public AbilityId Ability;
    }

    public struct AbilityEndedEvent
    {
        public AbilityId Ability;
        public bool Cancelled;
    }

    public struct AbilityBlockedEvent
    {
        public AbilityId Ability;
        public AbilityBlockReason Reason;
    }

    // ─────────────────────────────────────────────
    // Stamina
    // ─────────────────────────────────────────────

    public struct StaminaChangedEvent
    {
        public float Current;      // 0..MaxOrbs, fractional
        public int FullOrbs;       // usable orbs (floor)
        public int MaxOrbs;
    }

    public struct StaminaInsufficientEvent
    {
        public AbilityId Ability;
        public int Needed;
        public int Have;
    }

    // ─────────────────────────────────────────────
    // Health / Hazards
    // ─────────────────────────────────────────────

    public enum DamageKind
    {
        Toxic,
        Contact,
        Other
    }

    public enum ToxicSeverity
    {
        None,
        Light,
        Heavy
    }

    public struct SettingsChangedEvent
    {
        public GameSettings Settings;
    }

    public struct PlayerHealthChangedEvent
    {
        public float Current;
        public float Max;
        public float Delta; // negative = damage, positive = heal
    }

    public struct PlayerDiedEvent
    {
        public Vector3 Position;
        public string SectionId;
    }

    public struct PlayerRespawnedEvent
    {
        public string CheckpointId;
        public bool IsResume; // placed at a checkpoint when loading a save — not a death
    }

    public struct CheckpointReachedEvent
    {
        public string CheckpointId;
        public string SectionId;
    }

    public struct ToxicExposureChangedEvent
    {
        public ToxicSeverity Severity;
        public bool SlowsStaminaRegen; // false for level-wide zone pollution (only local toxic pockets slow regen)
    }

    // ─────────────────────────────────────────────
    // Dugong Companion
    // ─────────────────────────────────────────────

    public struct DugongMountChangedEvent
    {
        public bool Mounted;
    }

    // ─────────────────────────────────────────────
    // Dialogue
    // ─────────────────────────────────────────────

    public struct DialogueStartedEvent { }

    public struct DialogueEndedEvent { }

    // ─────────────────────────────────────────────
    // Level Flow / Navigation
    // ─────────────────────────────────────────────

    /// <summary>The "where to go next" target changed (null Target = no objective).</summary>
    public struct ObjectiveChangedEvent
    {
        public Transform Target;
    }

    public struct QuestStepCompletedEvent
    {
        public string StepId;
        public string ZoneId;
    }

    /// <summary>Level-wide cleanliness (0 = fully polluted, 1 = fully clean) target changed.</summary>
    public struct CleanlinessChangedEvent
    {
        public float Target;
        public bool Instant;
    }

    public struct LevelCompletedEvent
    {
        public Region Region;
    }

}
